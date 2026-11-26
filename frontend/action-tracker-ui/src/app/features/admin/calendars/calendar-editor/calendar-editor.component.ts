import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkCalendarService } from '../../../../services/work-calendar.service';
import {
  CALENDAR_LIMITS,
  CALENDAR_TIME_ZONE_OPTIONS,
  DEFAULT_CALENDAR_TIME_ZONE,
  WEEK_DAY_LABELS,
  WEEK_DAYS_MONDAY_FIRST,
  WeekDayName,
  WorkCalendar,
  WorkCalendarDay,
  WorkCalendarException,
  WorkCalendarInput,
  WorkCalendarUsage,
} from '../../../../models/work-calendar.model';
import {
  compareTimes,
  isIsoDate,
  toInputTime,
  toWireTime,
  todayIso,
} from '../../../../shared/utils/work-calendar.util';
import { CalendarMonthPreviewComponent } from '../calendar-month-preview/calendar-month-preview.component';
import {
  apiErrorMessage,
  exceptionKindLabel,
  formatDateRange,
  hoursLabel,
  rangeLength,
} from '../calendar-ui';
import { showErrorHere } from '../../../../core/http/server-reason';

type DayForm = FormGroup<{
  dayOfWeek: FormControl<WeekDayName>;
  isWorkingDay: FormControl<boolean>;
  startTime: FormControl<string>;
  endTime: FormControl<string>;
}>;

type ExceptionForm = FormGroup<{
  name: FormControl<string>;
  startDate: FormControl<string>;
  endDate: FormControl<string>;
  isWorkingDay: FormControl<boolean>;
  startTime: FormControl<string>;
  endTime: FormControl<string>;
}>;

/** An exception row held in the editor; `key` identifies it before it has a server id. */
interface ExceptionDraft extends WorkCalendarException {
  key: number;
}

const DEFAULT_START = '08:00';
const DEFAULT_END   = '17:00';

// ── Validators (mirror WorkCalendarInputValidator on the server) ─────────────

const notBlank: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  typeof c.value === 'string' && c.value.trim().length === 0 ? { required: true } : null;

/** A working day needs a start time earlier than its end time. */
const workingHours: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const { isWorkingDay, startTime, endTime } = (group as DayForm).getRawValue();
  if (!isWorkingDay) return null;
  return startTime && endTime && compareTimes(startTime, endTime) < 0 ? null : { hours: true };
};

/** At least one day of the week must be a working day. */
const atLeastOneWorkingDay: ValidatorFn = (array: AbstractControl): ValidationErrors | null =>
  (array as FormArray<DayForm>).getRawValue().some(d => d.isWorkingDay) ? null : { noWorkingDay: true };

/** End date on or after the start date; hours on a working exception are optional but complete and ordered. */
const exceptionRules: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const v = (group as ExceptionForm).getRawValue();
  const errors: ValidationErrors = {};
  if (isIsoDate(v.startDate) && isIsoDate(v.endDate) && v.endDate < v.startDate) errors['range'] = true;
  if (v.isWorkingDay) {
    const hasStart = !!v.startTime;
    const hasEnd   = !!v.endTime;
    if (hasStart !== hasEnd || (hasStart && hasEnd && compareTimes(v.startTime, v.endTime) >= 0)) {
      errors['hours'] = true;
    }
  }
  return Object.keys(errors).length ? errors : null;
};

/**
 * Right-hand drawer to create, edit or view a working calendar:
 * details, the weekly pattern, exceptions, and a month preview of the unsaved values.
 * Mounted by `CalendarsComponent` while open; emits `saved` with the server's copy.
 */
@Component({
  selector: 'app-calendar-editor',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CalendarMonthPreviewComponent],
  templateUrl: './calendar-editor.component.html',
  styleUrl: './calendar-editor.component.scss',
})
export class CalendarEditorComponent implements OnInit, AfterViewInit {
  private readonly fb         = inject(NonNullableFormBuilder);
  private readonly service    = inject(WorkCalendarService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector   = inject(Injector);

  /** The calendar to edit, or null to create a new one. */
  readonly calendar = input<WorkCalendar | null>(null);
  /** Usage of the calendar being edited (drives the "cannot deactivate" guard). */
  readonly usage    = input<WorkCalendarUsage | null>(null);
  /** False = read-only view (the user lacks Calendars.Edit / Calendars.Create). */
  readonly canEdit  = input(false);

  readonly closed = output<void>();
  readonly saved  = output<WorkCalendar>();

  private readonly drawerEl  = viewChild.required<ElementRef<HTMLElement>>('drawer');
  private readonly titleEl   = viewChild.required<ElementRef<HTMLElement>>('titleRef');

  readonly limits    = CALENDAR_LIMITS;
  readonly dayLabels = WEEK_DAY_LABELS;

  readonly form = this.fb.group({
    name:        ['', [Validators.required, notBlank, Validators.maxLength(CALENDAR_LIMITS.nameMax)]],
    description: ['', [Validators.maxLength(CALENDAR_LIMITS.descriptionMax)]],
    timeZoneId:  [DEFAULT_CALENDAR_TIME_ZONE, [Validators.required]],
    isActive:    [true],
    days:        this.fb.array<DayForm>([], { validators: atLeastOneWorkingDay }),
  });

  readonly exceptionForm: ExceptionForm = this.fb.group(
    {
      name:         ['', [Validators.required, notBlank, Validators.maxLength(CALENDAR_LIMITS.exceptionNameMax)]],
      startDate:    ['', [Validators.required]],
      endDate:      ['', [Validators.required]],
      isWorkingDay: [false],
      startTime:    [''],
      endTime:      [''],
    },
    { validators: exceptionRules },
  );

  readonly exceptions      = signal<ExceptionDraft[]>([]);
  /** `'new'` while adding, the row key while editing, null when the exception form is closed. */
  readonly editingKey      = signal<number | 'new' | null>(null);
  readonly exceptionTried  = signal(false);
  readonly submitted       = signal(false);
  readonly saving          = signal(false);
  readonly serverError     = signal<string | null>(null);
  readonly confirmDiscard  = signal(false);
  readonly previewMonth    = signal(todayIso().slice(0, 7));

  /** Live values for the preview — the form's raw values, not the saved calendar. */
  readonly previewDays     = signal<WorkCalendarDay[]>([]);
  private readonly draftException = signal<WorkCalendarException | null>(null);
  private readonly exceptionsDirty = signal(false);
  private nextKey = 1;

  readonly isNew    = computed(() => this.calendar() === null);
  readonly readOnly = computed(() => !this.canEdit());

  readonly title = computed(() => {
    if (this.isNew()) return 'New calendar';
    return this.readOnly() ? 'View calendar' : 'Edit calendar';
  });

  readonly timeZones = computed(() => {
    const current = this.calendar()?.timeZoneId;
    return current && !CALENDAR_TIME_ZONE_OPTIONS.some(o => o.id === current)
      ? [...CALENDAR_TIME_ZONE_OPTIONS, { id: current, label: current }]
      : CALENDAR_TIME_ZONE_OPTIONS;
  });

  /** Why the Active switch cannot be turned off, or null when it can. */
  readonly activeLockReason = computed<string | null>(() => {
    const c = this.calendar();
    if (!c || !c.isActive) return null;
    if (c.isDefault) return 'The default calendar must stay active.';
    const used = this.usage()?.totalWorkspaceCount ?? 0;
    return used > 0
      ? `Used by ${used} workspace${used === 1 ? '' : 's'}, so it cannot be deactivated.`
      : null;
  });

  /** The committed rows plus the one being typed, so the preview shows it before "Add". */
  readonly previewExceptions = computed<WorkCalendarException[]>(() => {
    const editing = this.editingKey();
    const draft   = this.draftException();
    if (!draft) return this.exceptions();
    return [...this.exceptions().filter(e => e.key !== editing), draft];
  });

  get days(): FormArray<DayForm> {
    return this.form.controls.days;
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    const c = this.calendar();

    this.form.patchValue({
      name:        c?.name ?? '',
      description: c?.description ?? '',
      timeZoneId:  c?.timeZoneId || DEFAULT_CALENDAR_TIME_ZONE,
      isActive:    c?.isActive ?? true,
    });

    for (const name of WEEK_DAYS_MONDAY_FIRST) {
      const row = c
        ? c.days.find(d => String(d.dayOfWeek).toLowerCase() === name)
        : this.newCalendarDay(name);
      this.days.push(this.createDayGroup(name, row));
    }

    this.exceptions.set(
      [...(c?.exceptions ?? [])]
        .sort((a, b) => a.startDate.localeCompare(b.startDate) || a.name.localeCompare(b.name))
        .map(e => ({ ...e, key: this.nextKey++ })),
    );

    // Start the preview on the month of the next upcoming exception, if any.
    const upcoming = this.exceptions().find(e => e.endDate >= todayIso());
    if (upcoming && upcoming.startDate > todayIso()) this.previewMonth.set(upcoming.startDate.slice(0, 7));

    if (this.readOnly()) {
      this.form.disable({ emitEvent: false });
    } else if (this.activeLockReason()) {
      this.form.controls.isActive.disable({ emitEvent: false });
    }

    this.updatePreviewDays();
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updatePreviewDays());

    this.exceptionForm.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.updateDraftException());

    this.exceptionForm.controls.isWorkingDay.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(working => {
        if (!working) this.exceptionForm.patchValue({ startTime: '', endTime: '' }, { emitEvent: false });
      });

    this.exceptionForm.controls.startDate.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(start => {
        const end = this.exceptionForm.controls.endDate;
        if (isIsoDate(start) && (!end.value || end.value < start)) end.setValue(start);
      });
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => this.titleEl().nativeElement.focus());
  }

  // ── Weekly pattern ────────────────────────────────────────────────────────

  private newCalendarDay(name: WeekDayName): WorkCalendarDay {
    const working = name !== 'saturday' && name !== 'sunday';
    return {
      dayOfWeek: name,
      isWorkingDay: working,
      startTime: working ? `${DEFAULT_START}:00` : null,
      endTime: working ? `${DEFAULT_END}:00` : null,
    };
  }

  private createDayGroup(name: WeekDayName, row: WorkCalendarDay | undefined): DayForm {
    const working = !!row?.isWorkingDay;
    const group: DayForm = this.fb.group(
      {
        dayOfWeek:    this.fb.control<WeekDayName>(name),
        isWorkingDay: this.fb.control(working),
        startTime:    this.fb.control({ value: toInputTime(row?.startTime), disabled: !working }),
        endTime:      this.fb.control({ value: toInputTime(row?.endTime), disabled: !working }),
      },
      { validators: workingHours },
    );

    group.controls.isWorkingDay.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(isWorking => this.onWorkingToggled(group, isWorking));

    return group;
  }

  private onWorkingToggled(group: DayForm, isWorking: boolean): void {
    const { startTime, endTime } = group.controls;
    if (isWorking) {
      if (!startTime.value && !endTime.value) {
        // Offer the hours of the first other working day, else 08:00–17:00.
        const other = this.days.controls.find(g => g !== group && g.controls.isWorkingDay.value);
        startTime.setValue(other?.controls.startTime.value || DEFAULT_START);
        endTime.setValue(other?.controls.endTime.value || DEFAULT_END);
      }
      startTime.enable();
      endTime.enable();
    } else {
      startTime.disable();
      endTime.disable();
    }
  }

  dayName(group: DayForm): WeekDayName {
    return group.controls.dayOfWeek.value;
  }

  showDayError(group: DayForm): boolean {
    return group.invalid && (this.submitted() || group.touched || group.dirty);
  }

  showError(control: AbstractControl): boolean {
    return control.invalid && (this.submitted() || control.touched);
  }

  private updatePreviewDays(): void {
    this.previewDays.set(
      this.days.getRawValue().map(d => ({
        dayOfWeek:    d.dayOfWeek,
        isWorkingDay: d.isWorkingDay,
        startTime:    d.isWorkingDay ? toWireTime(d.startTime) : null,
        endTime:      d.isWorkingDay ? toWireTime(d.endTime) : null,
      })),
    );
  }

  // ── Exceptions ────────────────────────────────────────────────────────────

  startAddException(): void {
    this.exceptionTried.set(false);
    this.editingKey.set('new');
    this.exceptionForm.reset({
      name: '', startDate: '', endDate: '', isWorkingDay: false, startTime: '', endTime: '',
    });
    this.focusSoon('#ce-ex-name');
  }

  startEditException(row: ExceptionDraft): void {
    this.exceptionTried.set(false);
    this.editingKey.set(row.key);
    this.exceptionForm.reset({
      name:         row.name,
      startDate:    row.startDate,
      endDate:      row.endDate,
      isWorkingDay: row.isWorkingDay,
      startTime:    toInputTime(row.startTime),
      endTime:      toInputTime(row.endTime),
    });
    this.previewMonth.set(row.startDate.slice(0, 7));
    this.focusSoon('#ce-ex-name');
  }

  commitException(): void {
    this.exceptionTried.set(true);
    this.exceptionForm.markAllAsTouched();
    if (this.exceptionForm.invalid) {
      this.focusFirstInvalid('.ce-ex-form');
      return;
    }

    const v = this.exceptionForm.getRawValue();
    const editing = this.editingKey();
    const existing = typeof editing === 'number' ? this.exceptions().find(e => e.key === editing) : undefined;

    const row: ExceptionDraft = {
      key:          existing?.key ?? this.nextKey++,
      id:           existing?.id ?? null,
      name:         v.name.trim(),
      startDate:    v.startDate,
      endDate:      v.endDate,
      isWorkingDay: v.isWorkingDay,
      startTime:    v.isWorkingDay ? toWireTime(v.startTime) : null,
      endTime:      v.isWorkingDay ? toWireTime(v.endTime) : null,
    };

    this.exceptions.update(list =>
      [...list.filter(e => e.key !== row.key), row]
        .sort((a, b) => a.startDate.localeCompare(b.startDate) || a.name.localeCompare(b.name)));
    this.exceptionsDirty.set(true);
    this.closeExceptionForm();
    this.previewMonth.set(row.startDate.slice(0, 7));
    this.focusSoon('#ce-ex-add');
  }

  cancelException(): void {
    this.closeExceptionForm();
    this.focusSoon('#ce-ex-add');
  }

  removeException(row: ExceptionDraft): void {
    if (this.editingKey() === row.key) this.closeExceptionForm();
    this.exceptions.update(list => list.filter(e => e.key !== row.key));
    this.exceptionsDirty.set(true);
    this.focusSoon('#ce-ex-add');
  }

  private closeExceptionForm(): void {
    this.editingKey.set(null);
    this.exceptionTried.set(false);
    this.draftException.set(null);
  }

  private updateDraftException(): void {
    if (this.editingKey() === null) {
      this.draftException.set(null);
      return;
    }
    const v = this.exceptionForm.getRawValue();
    const complete = isIsoDate(v.startDate) && isIsoDate(v.endDate) && v.endDate >= v.startDate;
    this.draftException.set(complete
      ? {
          name:         v.name.trim() || 'New exception',
          startDate:    v.startDate,
          endDate:      v.endDate,
          isWorkingDay: v.isWorkingDay,
          startTime:    v.isWorkingDay ? toWireTime(v.startTime) : null,
          endTime:      v.isWorkingDay ? toWireTime(v.endTime) : null,
        }
      : null);
  }

  showExceptionError(control: AbstractControl): boolean {
    return control.invalid && (this.exceptionTried() || control.touched);
  }

  trackException = (_: number, e: ExceptionDraft) => e.key;

  readonly kindLabel  = exceptionKindLabel;
  readonly dateRange  = formatDateRange;
  readonly lengthOf   = rangeLength;
  readonly hours      = hoursLabel;

  // ── Save / close ──────────────────────────────────────────────────────────

  isDirty(): boolean {
    return this.form.dirty || this.exceptionsDirty() || this.editingKey() !== null;
  }

  save(): void {
    if (this.readOnly() || this.saving()) return;
    this.submitted.set(true);
    this.serverError.set(null);
    this.form.markAllAsTouched();

    if (this.editingKey() !== null) {
      this.serverError.set('Add or cancel the exception you are editing before saving.');
      this.focusSoon('#ce-ex-name');
      return;
    }
    if (this.form.invalid) {
      this.focusFirstInvalid('.ce-main-form');
      return;
    }

    const raw = this.form.getRawValue();
    const dto: WorkCalendarInput = {
      name:        raw.name.trim(),
      description: raw.description.trim() || null,
      timeZoneId:  raw.timeZoneId,
      isActive:    raw.isActive,
      days: raw.days.map(d => ({
        dayOfWeek:    d.dayOfWeek,
        isWorkingDay: d.isWorkingDay,
        startTime:    d.isWorkingDay ? toWireTime(d.startTime) : null,
        endTime:      d.isWorkingDay ? toWireTime(d.endTime) : null,
      })),
      exceptions: this.exceptions().map(e => ({
        id:           e.id ?? null,
        name:         e.name,
        startDate:    e.startDate,
        endDate:      e.endDate,
        isWorkingDay: e.isWorkingDay,
        startTime:    e.isWorkingDay ? e.startTime ?? null : null,
        endTime:      e.isWorkingDay ? e.endTime ?? null : null,
      })),
    };

    const existing = this.calendar();
    const request = existing ? this.service.update(existing.id, dto) : this.service.create(dto);

    this.saving.set(true);
    request.subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success && res.data) {
          this.form.markAsPristine();
          this.exceptionsDirty.set(false);
          this.saved.emit(res.data);
        } else {
          this.showServerError(res.message || 'The calendar could not be saved.');
        }
      },
      error: err => {
        this.saving.set(false);
        this.showServerError(showErrorHere(err) ?? apiErrorMessage(err, 'The calendar could not be saved.'));
      },
    });
  }

  requestClose(): void {
    if (this.saving()) return;
    if (!this.readOnly() && this.isDirty()) {
      this.confirmDiscard.set(true);
      this.focusSoon('#ce-keep-editing');
      return;
    }
    this.closed.emit();
  }

  keepEditing(): void {
    this.confirmDiscard.set(false);
    this.focusSoon('#ce-save');
  }

  discard(): void {
    this.closed.emit();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      if (this.confirmDiscard()) this.keepEditing();
      else if (this.editingKey() !== null) this.cancelException();
      else this.requestClose();
      return;
    }
    if (event.key === 'Tab') this.trapFocus(event);
  }

  private showServerError(message: string): void {
    this.serverError.set(message);
    this.focusSoon('#ce-server-error');
  }

  // ── Focus helpers ─────────────────────────────────────────────────────────

  /** Keeps Tab inside the drawer while it is open (it is a modal dialog). */
  private trapFocus(event: KeyboardEvent): void {
    const focusable = Array.from(
      this.drawerEl().nativeElement.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ),
    ).filter(el => el.offsetParent !== null);
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last  = focusable[focusable.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && (active === first || active === this.titleEl().nativeElement)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  /**
   * Focuses an element once the view has rendered. The app coalesces change
   * detection to the next animation frame (`eventCoalescing: true`), so a plain
   * `setTimeout` would run before an element shown by the same event exists.
   */
  private focusSoon(selector: string): void {
    afterNextRender(
      () => this.drawerEl().nativeElement.querySelector<HTMLElement>(selector)?.focus(),
      { injector: this.injector },
    );
  }

  private focusFirstInvalid(scope: string): void {
    afterNextRender(() => {
      const root = this.drawerEl().nativeElement.querySelector(scope);
      root?.querySelector<HTMLElement>('input.ng-invalid, select.ng-invalid, textarea.ng-invalid, .is-invalid')?.focus();
    }, { injector: this.injector });
  }
}
