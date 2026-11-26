import {
  ChangeDetectionStrategy, Component, DestroyRef, ElementRef, HostListener, Injector,
  afterNextRender, computed, effect, inject, input, signal, untracked, viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { map } from 'rxjs/operators';

import { ActionItem, AssignableUser } from '../../../../core/models/action-item.model';
import { ToastService } from '../../../../core/services/toast.service';
import { AuthService } from '../../../../core/services/auth.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { ActionItemSubItemService } from '../../../../services/action-item-sub-item.service';
import { ActionUpdateService } from '../../../../services/action-update.service';
import { injectWorkspaceCalendar } from '../../../../services/workspace-calendar.service';
import {
  ActionItemSubItem, ActionItemSubItemInput, ActionItemSubItemList,
} from '../../../../models/action-item-sub-item.model';
import { HasPermissionDirective } from '../../../../shared';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { WorkingDayHintComponent } from '../../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../../shared/components/working-days/working-days.component';
import { SubItemCountComponent } from '../../../../shared/components/sub-item-count/sub-item-count.component';
import { nonWorkingDatesMessage, workingDayValidator } from '../../../../shared/validators/working-day.validator';
import { subItemDateProblem, toDay } from '../../../../shared/utils/sub-item-dates.util';
import { ActionUpdatesSectionComponent } from '../action-updates-section/action-updates-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import { showErrorHere } from '../../../../core/http/server-reason';

/** Title that is not just blanks. */
const notBlank: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  typeof c.value === 'string' && c.value.trim().length > 0 ? null : { required: true };

/**
 * Sub-items of an action item (WO-2026-11 A-2), on the action detail page:
 * list with done checkboxes, assignee chips and dates; inline add; an edit
 * drawer (title, description, dates, assignees); reorder with up / down
 * buttons; delete through the shared confirm dialog. Writes need
 * `ActionItems.Edit`; a user assigned to a sub-item may also tick it.
 * Nothing can be changed while the parent is Done or Cancelled (the server
 * answers 409). Sub-items left outside the parent's planned dates after those
 * dates changed are named in a warning at the top (decision D3).
 *
 * Each row has an "Updates (n)" toggle (WO-2026-11 A-3) that opens the
 * sub-item's action updates under it; updates stay possible while the parent
 * is Done or Cancelled.
 *
 * A "Files (n)" toggle (WO-2026-11 A-4) opens the sub-item's files; adding and
 * removing them needs `ActionItems.Edit` and stops while the parent is Done /
 * Cancelled (the server answers 409); delete buttons follow the server's `canDelete`.
 *
 * Date rules — the server stays the authority:
 *  - within the parent's planned dates: `sub-item-dates.util.ts` (copy of
 *    `ActionItemSubItemRules`);
 *  - working days of the workspace calendar: `workingDayValidator` and
 *    `<app-working-day-hint>` (C-3). Unchanged dates are never flagged.
 */
@Component({
  selector: 'app-sub-items-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe, ReactiveFormsModule, HasPermissionDirective, ConfirmDialogComponent,
    WorkingDayHintComponent, WorkingDaysComponent, SubItemCountComponent, ActionUpdatesSectionComponent,
    DocumentsSectionComponent,
  ],
  templateUrl: './sub-items-section.component.html',
  styleUrl: './sub-items-section.component.scss',
})
export class SubItemsSectionComponent {
  /** The parent; the list reloads when its id, planned dates or status change. */
  readonly actionItem = input.required<ActionItem>();

  private readonly service      = inject(ActionItemSubItemService);
  private readonly updateSvc    = inject(ActionUpdateService);
  private readonly toast        = inject(ToastService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly permState    = inject(PermissionStateService);
  private readonly auth         = inject(AuthService);
  private readonly fb           = inject(FormBuilder);
  private readonly injector     = inject(Injector);
  private readonly destroyRef   = inject(DestroyRef);
  private readonly host         = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly confirmDialog = viewChild<ConfirmDialogComponent>('deleteDialog');

  // ── State ───────────────────────────────────────────────────────────────
  readonly list       = signal<ActionItemSubItemList | null>(null);
  readonly loading    = signal(true);
  readonly loadFailed = signal(false);
  /** Id of the row whose action is in flight (disables its controls). */
  readonly busyId     = signal<string | null>(null);
  readonly adding     = signal(false);

  readonly items          = computed(() => this.list()?.items ?? []);
  readonly outsideItems   = computed(() => this.items().filter(i => i.isOutsideParentDates));
  readonly acceptsChanges = computed(() => this.list()?.parentAcceptsChanges ?? false);

  private readonly permissions = toSignal(this.permState.permissions$, { initialValue: [] });
  readonly canView = computed(() => { this.permissions(); return this.permState.hasPermission('ActionItems', 'View'); });
  readonly canEdit = computed(() => { this.permissions(); return this.permState.hasPermission('ActionItems', 'Edit'); });
  private readonly myUserId = toSignal(this.auth.currentUser$.pipe(map(u => u?.userId ?? null)), { initialValue: null });

  readonly workspaceId = computed(() => this.actionItem().workspaceId);
  readonly calendar    = injectWorkspaceCalendar(() => this.workspaceId());

  private readonly reloadKey = computed(() => {
    const a = this.actionItem();
    return `${a.id}|${a.startDate}|${a.dueDate}|${a.statusCode}`;
  });

  // ── Inline add ──────────────────────────────────────────────────────────
  readonly quickForm = this.fb.nonNullable.group({
    title:     ['', [notBlank, Validators.maxLength(255)]],
    startDate: ['', workingDayValidator(() => this.calendar(), () => null, 'Sub-item start')],
    endDate:   ['', [Validators.required, workingDayValidator(() => this.calendar(), () => null, 'Sub-item end')]],
  }, { validators: [this.rangeValidator()] });

  // ── Drawer (create with details / edit) ─────────────────────────────────
  readonly drawerOpen  = signal(false);
  /** The sub-item being edited; null while creating. */
  readonly editing     = signal<ActionItemSubItem | null>(null);
  readonly saving      = signal(false);
  readonly serverError = signal<string | null>(null);
  readonly assignableUsers = signal<AssignableUser[]>([]);
  readonly assigneeFilter  = signal('');
  private  usersLoadedFor: string | null = null;
  private  returnFocusTo: HTMLElement | null = null;

  readonly editForm = this.fb.nonNullable.group({
    title:       ['', [notBlank, Validators.maxLength(255)]],
    description: ['', Validators.maxLength(5000)],
    startDate:   ['', workingDayValidator(() => this.calendar(), () => toDay(this.editing()?.startDate), 'Sub-item start')],
    endDate:     ['', [Validators.required,
                       workingDayValidator(() => this.calendar(), () => toDay(this.editing()?.endDate), 'Sub-item end')]],
    assigneeIds: [[] as string[]],
  }, { validators: [this.rangeValidator()] });

  /** Selected assignees, plus anyone already assigned who has left the workspace's lists. */
  readonly selectedAssigneeIds = signal<string[]>([]);
  readonly assigneeOptions = computed(() => {
    const known = new Map(this.assignableUsers().map(u => [u.userId, u] as const));
    for (const a of this.editing()?.assignees ?? []) {
      if (!known.has(a.userId)) known.set(a.userId, { userId: a.userId, userName: a.fullName, email: a.email, sources: [] });
    }
    const term = this.assigneeFilter().trim().toLowerCase();
    return [...known.values()]
      .filter(u => !term || u.userName.toLowerCase().includes(term) || (u.email ?? '').toLowerCase().includes(term)
                 || (u.orgUnitName ?? '').toLowerCase().includes(term))
      .sort((x, y) => x.userName.localeCompare(y.userName));
  });

  // ── Delete ──────────────────────────────────────────────────────────────
  readonly pendingDelete = signal<ActionItemSubItem | null>(null);

  // ── Action updates per sub-item (WO-2026-11 A-3) ────────────────────────
  /** Updates the caller can see per sub-item (submitted + own drafts); absent = 0. */
  readonly updateCounts = signal<ReadonlyMap<string, number>>(new Map());
  /** Sub-items whose updates panel is open. */
  readonly openUpdates  = signal<ReadonlySet<string>>(new Set());

  // ── Files per sub-item (WO-2026-11 A-4) ─────────────────────────────────
  /** Counts reported by open file panels; otherwise the sub-item's `documentCount`. */
  readonly fileCounts = signal<ReadonlyMap<string, number>>(new Map());
  readonly openFiles  = signal<ReadonlySet<string>>(new Set());
  readonly canUploadFiles = computed(() => this.canEdit() && this.acceptsChanges());
  readonly filesLockedNote = computed(() => this.list() && !this.acceptsChanges()
    ? `This action item is ${this.actionItem().statusLabel}, so files on its sub-items can no longer be added or removed.`
    : null);

  constructor() {
    effect(() => {
      this.reloadKey();
      if (!this.canView()) { this.loading.set(false); return; }
      untracked(() => this.load());
    });
    // Re-run the working-day validators once the calendar arrives.
    effect(() => {
      this.calendar();
      untracked(() => {
        for (const form of [this.quickForm, this.editForm]) {
          form.controls.startDate.updateValueAndValidity({ emitEvent: false });
          form.controls.endDate.updateValueAndValidity({ emitEvent: false });
        }
      });
    });
  }

  // ── Loading ─────────────────────────────────────────────────────────────
  load(): void {
    const id = this.actionItem().id;
    this.loading.set(true);
    this.loadFailed.set(false);
    this.service.list(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.list.set(r.data);
        this.loading.set(false);
        this.revalidate();
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
    this.loadUpdateCounts(id);
  }

  private loadUpdateCounts(actionItemId: string): void {
    this.updateSvc.counts(actionItemId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.updateCounts.set(new Map(
        (r.data?.subItems ?? []).map(c => [c.subItemId, c.submittedCount + c.draftCount] as const))),
      error: () => this.updateCounts.set(new Map()),
    });
  }

  updateCount(subItemId: string): number {
    return this.updateCounts().get(subItemId) ?? 0;
  }

  updatesOpen(subItemId: string): boolean {
    return this.openUpdates().has(subItemId);
  }

  toggleUpdates(subItemId: string): void {
    const next = new Set(this.openUpdates());
    if (next.has(subItemId)) next.delete(subItemId); else next.add(subItemId);
    this.openUpdates.set(next);
  }

  fileCount(s: ActionItemSubItem): number {
    return this.fileCounts().get(s.id) ?? s.documentCount ?? 0;
  }

  filesOpen(subItemId: string): boolean {
    return this.openFiles().has(subItemId);
  }

  toggleFiles(subItemId: string): void {
    const next = new Set(this.openFiles());
    if (next.has(subItemId)) next.delete(subItemId); else next.add(subItemId);
    this.openFiles.set(next);
  }

  setFileCount(subItemId: string, count: number): void {
    if (this.fileCounts().get(subItemId) === count) return;
    const next = new Map(this.fileCounts());
    next.set(subItemId, count);
    this.fileCounts.set(next);
  }

  /** The open panel reports its count whenever its list changes. */
  setUpdateCount(subItemId: string, count: number): void {
    if (this.updateCount(subItemId) === count) return;
    const next = new Map(this.updateCounts());
    next.set(subItemId, count);
    this.updateCounts.set(next);
  }

  private revalidate(): void {
    this.quickForm.updateValueAndValidity({ emitEvent: false });
    this.editForm.updateValueAndValidity({ emitEvent: false });
  }

  // ── Permissions per row ─────────────────────────────────────────────────
  canTick(s: ActionItemSubItem): boolean {
    if (!this.acceptsChanges()) return false;
    const me = this.myUserId();
    return this.canEdit() || (!!me && s.assignees.some(a => a.userId === me));
  }

  // ── Date rule (shared by both forms) ────────────────────────────────────
  private rangeValidator(): ValidatorFn {
    return (group: AbstractControl): ValidationErrors | null => {
      const list = this.list?.();
      if (!list) return null;
      const start = group.get('startDate')?.value as string;
      const end   = group.get('endDate')?.value as string;
      // On edit the range rule applies only when a date changes (server rule).
      const editing = group === this.editForm ? this.editing() : null;
      if (editing && toDay(editing.startDate) === toDay(start) && toDay(editing.endDate) === toDay(end)) return null;
      const problem = subItemDateProblem({ start, end, parentStart: list.parentStartDate, parentEnd: list.parentDueDate });
      return problem ? { subItemRange: problem } : null;
    };
  }

  rangeProblem(form: 'quick' | 'edit'): string | null {
    const g = form === 'quick' ? this.quickForm : this.editForm;
    return (g.errors?.['subItemRange'] as string | undefined) ?? null;
  }

  /** "Planned 3 Jun – 28 Jun 2030" bounds for the date inputs. */
  readonly minDate = computed(() => toDay(this.list()?.parentStartDate) ?? '');
  readonly maxDate = computed(() => toDay(this.list()?.parentDueDate) ?? '');

  // ── Inline add ──────────────────────────────────────────────────────────
  addQuick(): void {
    this.quickForm.markAllAsTouched();
    if (this.quickForm.invalid || this.adding()) return;
    const v = this.quickForm.getRawValue();
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Sub-item start', value: v.startDate },
      { field: 'Sub-item end',   value: v.endDate },
    ]);
    if (dateProblem) { this.toast.error(dateProblem); return; }

    this.adding.set(true);
    this.service.create(this.actionItem().id, {
      title: v.title.trim(), description: null, startDate: v.startDate || null, endDate: v.endDate, assigneeIds: [],
    }).subscribe({
      next: () => {
        this.adding.set(false);
        this.quickForm.reset({ title: '', startDate: v.startDate, endDate: v.endDate });
        this.toast.success('Sub-item added.');
        this.load();
        afterNextRender(() => this.focus('si-quick-title'), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.adding.set(false);
        this.reportError(err, 'Could not add the sub-item.');
      },
    });
  }

  // ── Done ────────────────────────────────────────────────────────────────
  toggleDone(s: ActionItemSubItem, event: Event): void {
    const checkbox = event.target as HTMLInputElement;
    const wanted = checkbox.checked;
    if (!this.canTick(s)) { checkbox.checked = s.isDone; return; }

    this.busyId.set(s.id);
    this.service.setDone(this.actionItem().id, s.id, wanted).subscribe({
      next: r => {
        this.busyId.set(null);
        if (r?.data) this.replaceItem(r.data);
        afterNextRender(() => this.focus(`si-done-${s.id}`), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        checkbox.checked = s.isDone;
        this.reportError(err, 'Could not update the sub-item.');
      },
    });
  }

  private replaceItem(updated: ActionItemSubItem): void {
    const list = this.list();
    if (!list) return;
    const items = list.items.map(i => i.id === updated.id ? updated : i);
    this.list.set({ ...list, items, doneCount: items.filter(i => i.isDone).length, totalCount: items.length });
  }

  // ── Reorder ─────────────────────────────────────────────────────────────
  move(s: ActionItemSubItem, delta: -1 | 1): void {
    const ids = this.items().map(i => i.id);
    const from = ids.indexOf(s.id);
    const to = from + delta;
    if (from < 0 || to < 0 || to >= ids.length) return;
    [ids[from], ids[to]] = [ids[to], ids[from]];

    this.busyId.set(s.id);
    this.service.reorder(this.actionItem().id, ids).subscribe({
      next: r => {
        this.busyId.set(null);
        if (r?.data) this.list.set(r.data);
        // Keep focus on the moved row; at the top / bottom use the other arrow.
        const edge = (delta < 0 && to === 0) || (delta > 0 && to === ids.length - 1);
        const target = edge ? (delta < 0 ? `si-down-${s.id}` : `si-up-${s.id}`) : (delta < 0 ? `si-up-${s.id}` : `si-down-${s.id}`);
        afterNextRender(() => this.focus(target), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        this.reportError(err, 'Could not reorder the sub-items.');
      },
    });
  }

  // ── Delete ──────────────────────────────────────────────────────────────
  askDelete(s: ActionItemSubItem, trigger: HTMLElement): void {
    this.pendingDelete.set(s);
    this.returnFocusTo = trigger;
    this.confirmDialog()?.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    const s = this.pendingDelete();
    this.pendingDelete.set(null);
    if (!confirmed || !s) { this.restoreFocus(); return; }

    const index = this.items().findIndex(i => i.id === s.id);
    this.busyId.set(s.id);
    this.service.delete(this.actionItem().id, s.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success('Sub-item deleted.');
        const list = this.list();
        if (list) {
          const items = list.items.filter(i => i.id !== s.id).map((i, n) => ({ ...i, sortOrder: n + 1 }));
          this.list.set({ ...list, items, totalCount: items.length, doneCount: items.filter(i => i.isDone).length });
        }
        // Focus the row that took its place, else the add field.
        const next = this.items()[Math.min(index, this.items().length - 1)];
        this.returnFocusTo = null;
        afterNextRender(() => this.focus(next ? `si-done-${next.id}` : 'si-quick-title') || this.focus('si-heading'),
          { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        this.reportError(err, 'Could not delete the sub-item.');
        this.restoreFocus();
      },
    });
  }

  // ── Drawer ──────────────────────────────────────────────────────────────
  openCreate(trigger: HTMLElement): void {
    const q = this.quickForm.getRawValue();
    this.openDrawer(null, trigger, {
      title: q.title, description: '', startDate: q.startDate, endDate: q.endDate, assigneeIds: [],
    });
  }

  openEdit(s: ActionItemSubItem, trigger: HTMLElement): void {
    this.openDrawer(s, trigger, {
      title: s.title, description: s.description ?? '',
      startDate: toDay(s.startDate) ?? '', endDate: toDay(s.endDate) ?? '',
      assigneeIds: s.assignees.map(a => a.userId),
    });
  }

  private openDrawer(s: ActionItemSubItem | null, trigger: HTMLElement,
                     value: { title: string; description: string; startDate: string; endDate: string; assigneeIds: string[] }): void {
    this.editing.set(s);
    this.returnFocusTo = trigger;
    this.serverError.set(null);
    this.assigneeFilter.set('');
    this.editForm.reset(value);
    this.selectedAssigneeIds.set([...value.assigneeIds]);
    this.editForm.updateValueAndValidity();
    this.loadAssignableUsers();
    this.drawerOpen.set(true);
    afterNextRender(() => this.focus('si-edit-title'), { injector: this.injector });
  }

  closeDrawer(): void {
    if (this.saving()) return;
    this.drawerOpen.set(false);
    this.editing.set(null);
    this.restoreFocus();
  }

  private loadAssignableUsers(): void {
    const ws = this.workspaceId();
    if (!ws || this.usersLoadedFor === ws) return;
    this.usersLoadedFor = ws;
    this.workspaceSvc.getAssignableUsers(ws).subscribe({
      next: r => this.assignableUsers.set(r.data ?? []),
      error: () => { this.usersLoadedFor = null; this.assignableUsers.set([]); },
    });
  }

  isSelected(userId: string): boolean {
    return this.selectedAssigneeIds().includes(userId);
  }

  toggleAssignee(userId: string): void {
    const ids = this.selectedAssigneeIds();
    const next = ids.includes(userId) ? ids.filter(i => i !== userId) : [...ids, userId];
    this.selectedAssigneeIds.set(next);
    this.editForm.controls.assigneeIds.setValue(next);
    this.editForm.markAsDirty();
  }

  assigneeName(userId: string): string {
    return this.assigneeOptionsAll().find(u => u.userId === userId)?.userName ?? 'Unknown user';
  }

  private assigneeOptionsAll(): AssignableUser[] {
    const all = [...this.assignableUsers()];
    for (const a of this.editing()?.assignees ?? []) {
      if (!all.some(u => u.userId === a.userId)) all.push({ userId: a.userId, userName: a.fullName, email: a.email, sources: [] });
    }
    return all;
  }

  save(): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid || this.saving()) return;
    const v = this.editForm.getRawValue();
    const editing = this.editing();
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Sub-item start', value: v.startDate, original: toDay(editing?.startDate) },
      { field: 'Sub-item end',   value: v.endDate,   original: toDay(editing?.endDate) },
    ]);
    if (dateProblem) { this.serverError.set(dateProblem); return; }

    const body: ActionItemSubItemInput = {
      title: v.title.trim(),
      description: v.description.trim() || null,
      startDate: v.startDate || null,
      endDate: v.endDate,
      assigneeIds: this.selectedAssigneeIds(),
    };
    const parentId = this.actionItem().id;
    const request = editing
      ? this.service.update(parentId, editing.id, body)
      : this.service.create(parentId, body);

    this.saving.set(true);
    this.serverError.set(null);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(editing ? 'Sub-item updated.' : 'Sub-item added.');
        if (!editing) this.quickForm.reset({ title: '', startDate: '', endDate: '' });
        this.drawerOpen.set(false);
        this.editing.set(null);
        this.load();
        this.restoreFocus();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.serverError.set(showErrorHere(err) ?? 'Could not save the sub-item.');
        afterNextRender(() => this.focus('si-edit-error'), { injector: this.injector });
      },
    });
  }

  // ── Keyboard: Esc closes the drawer, Tab stays inside it ────────────────
  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (!this.drawerOpen()) return;
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeDrawer();
      return;
    }
    if (event.key !== 'Tab') return;
    const drawer = (this.host.nativeElement as HTMLElement).querySelector<HTMLElement>('.si-drawer');
    if (!drawer) return;
    const focusable = Array.from(drawer.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'))
      .filter(el => el.offsetParent !== null);
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (event.shiftKey && (active === first || !drawer.contains(active))) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && (active === last || !drawer.contains(active))) { event.preventDefault(); first.focus(); }
  }

  // ── Helpers ─────────────────────────────────────────────────────────────
  private focus(id: string): boolean {
    const el = document.getElementById(id) as HTMLElement | null;
    if (!el || (el as HTMLButtonElement).disabled) return false;
    el.focus();
    return true;
  }

  private restoreFocus(): void {
    const target = this.returnFocusTo;
    this.returnFocusTo = null;
    if (target) afterNextRender(() => { if (document.contains(target)) target.focus(); else this.focus('si-heading'); },
      { injector: this.injector });
  }

  private errorText(err: HttpErrorResponse, fallback: string): string {
    const body = err?.error;
    if (typeof body?.message === 'string' && body.message) return body.message;
    if (body?.errors && typeof body.errors === 'object') {
      const all = Object.values(body.errors as Record<string, string[]>).flat().filter(Boolean);
      if (all.length) return all.join(' ');
    }
    return fallback;
  }

  /**
   * The error interceptor toasts a 400 / 403 / 404 itself; a 409 (parent Done or
   * Cancelled) is toasted here with the same reason (ngx-toastr drops the repeat)
   * and reloads to show the locked state.
   */
  private reportError(err: HttpErrorResponse, fallback: string): void {
    if (err.status === 409) {
      this.toast.error(this.errorText(err, fallback));
      this.load();
    } else if (err.status !== 400 && err.status !== 403 && err.status !== 404) {
      this.toast.error(fallback);
    }
  }

  // ── Display ─────────────────────────────────────────────────────────────
  private static readonly dayFormat = new Intl.DateTimeFormat('en-GB',
    { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });

  /** A calendar day as "3 Jun 2030", read as a calendar day (no time-zone shift). */
  formatDay(value: string | null | undefined): string {
    const day = toDay(value);
    return day ? SubItemsSectionComponent.dayFormat.format(new Date(day + 'T00:00:00Z')) : '';
  }

  /** "3 Jun 2030 – 7 Jun 2030", or "Ends 7 Jun 2030" without a start. */
  range(s: { startDate: string | null; endDate: string }): string {
    return s.startDate ? `${this.formatDay(s.startDate)} – ${this.formatDay(s.endDate)}` : `Ends ${this.formatDay(s.endDate)}`;
  }

  /** The parent's planned dates, "3 Jun 2030 – 28 Jun 2030" or "until 28 Jun 2030". */
  plannedRange(l: ActionItemSubItemList): string {
    return l.parentStartDate
      ? `${this.formatDay(l.parentStartDate)} – ${this.formatDay(l.parentDueDate)}`
      : `until ${this.formatDay(l.parentDueDate)}`;
  }

  initial(name: string | null | undefined): string {
    return (name ?? '?').trim().charAt(0).toUpperCase() || '?';
  }

  fieldError(form: 'quick' | 'edit', name: string): string | null {
    const group: AbstractControl = form === 'quick' ? this.quickForm : this.editForm;
    const c = group.get(name);
    if (!c || !c.errors || !(c.touched || c.dirty)) return null;
    if (c.errors['required']) return name === 'title' ? 'Title is required.' : 'Sub-item end is required.';
    if (c.errors['maxlength']) return `At most ${c.errors['maxlength'].requiredLength} characters.`;
    return null;
  }
}
