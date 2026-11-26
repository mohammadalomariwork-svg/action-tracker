import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { WorkCalendarService } from '../../../services/work-calendar.service';
import { WorkCalendar, WorkCalendarUsage } from '../../../models/work-calendar.model';
import { ToastService } from '../../../core/services/toast.service';
import { PermissionStateService } from '../../permissions/services/permission-state.service';
import { AdminBreadcrumbComponent } from '../components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { HasPermissionDirective } from '../../../shared';
import { todayIso } from '../../../shared/utils/work-calendar.util';
import { CalendarEditorComponent } from './calendar-editor/calendar-editor.component';
import {
  apiErrorMessage,
  formatDateRange,
  hoursByDay,
  nextException,
  summarizeHours,
  summarizeWorkingDays,
} from './calendar-ui';
import { showErrorHere } from '../../../core/http/server-reason';

/** `undefined` = not loaded yet, `null` = the usage call failed. */
type UsageState = WorkCalendarUsage | null | undefined;

type PendingAction =
  | { kind: 'default'; calendar: WorkCalendar }
  | { kind: 'delete'; calendar: WorkCalendar };

/**
 * Admin Panel → Calendars (`/admin/calendars`, WO-2026-11 C-2).
 * Lists the working calendars with their week, hours, exceptions and the
 * workspaces using them; create / edit in a drawer; set default; soft delete and restore.
 * Page pattern copied from `DataSyncComponent`.
 */
@Component({
  selector: 'app-calendars',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
    ConfirmDialogComponent,
    HasPermissionDirective,
    CalendarEditorComponent,
  ],
  templateUrl: './calendars.component.html',
  styleUrl: './calendars.component.scss',
})
export class CalendarsComponent implements OnInit {
  private readonly service     = inject(WorkCalendarService);
  private readonly toast       = inject(ToastService);
  private readonly permissions = inject(PermissionStateService);

  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  readonly loading     = signal(true);
  readonly refreshing  = signal(false);
  readonly loadFailed  = signal(false);
  readonly calendars   = signal<WorkCalendar[]>([]);
  readonly usage       = signal<Record<string, UsageState>>({});
  readonly showDeleted = signal(false);
  readonly busyId      = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  /** Open editor: `calendar` null = create. */
  readonly editor = signal<{ calendar: WorkCalendar | null } | null>(null);
  private editorTrigger: HTMLElement | null = null;
  private confirmTrigger: HTMLElement | null = null;

  readonly pending = signal<PendingAction | null>(null);

  readonly active   = computed(() => this.calendars().filter(c => !c.isDeleted));
  readonly deleted  = computed(() => this.calendars().filter(c => c.isDeleted));
  readonly defaultCalendar = computed(() => this.active().find(c => c.isDefault) ?? null);
  readonly activeCount     = computed(() => this.active().filter(c => c.isActive).length);
  readonly inheritingCount = computed(() => {
    const d = this.defaultCalendar();
    const u = d ? this.usage()[d.id] : undefined;
    return u ? u.inheritingWorkspaceCount : null;
  });

  readonly confirmTitle = computed(() => {
    const p = this.pending();
    if (!p) return 'Confirm';
    return p.kind === 'default' ? 'Make default calendar' : 'Delete calendar';
  });

  readonly confirmMessage = computed(() => {
    const p = this.pending();
    if (!p) return '';
    if (p.kind === 'delete') {
      return `Delete "${p.calendar.name}"? It is hidden from the list and can be restored later with "Show deleted".`;
    }
    const current = this.defaultCalendar();
    const inheriting = this.inheritingCount();
    const followers = inheriting === null
      ? 'Workspaces without a calendar of their own'
      : `${inheriting} workspace${inheriting === 1 ? '' : 's'} without a calendar of their own`;
    return `Make "${p.calendar.name}" the default calendar? ${followers} will follow it from now on`
      + (current ? `, and "${current.name}" stops being the default.` : '.');
  });

  readonly today = todayIso();

  ngOnInit(): void {
    this.load();
  }

  // ── Loading ───────────────────────────────────────────────────────────────

  load(silent = false): void {
    silent ? this.refreshing.set(true) : this.loading.set(true);

    this.service.getAll(this.showDeleted()).subscribe({
      next: res => {
        if (res.success) {
          this.calendars.set(res.data ?? []);
          this.loadFailed.set(false);
          this.loadUsage(res.data ?? []);
        } else {
          this.loadFailed.set(true);
          this.toast.error(res.message || 'Failed to load calendars.');
        }
        this.loading.set(false);
        this.refreshing.set(false);
      },
      // The error interceptor already shows a toast; keep a retry state on the page.
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
        this.refreshing.set(false);
      },
    });
  }

  private loadUsage(calendars: WorkCalendar[]): void {
    const live = calendars.filter(c => !c.isDeleted);
    if (live.length === 0) {
      this.usage.set({});
      return;
    }
    forkJoin(
      live.map(c =>
        this.service.getUsage(c.id).pipe(
          map(res => [c.id, res.success ? res.data : null] as const),
          catchError(() => of([c.id, null] as const)),
        ),
      ),
    ).subscribe(entries => this.usage.set(Object.fromEntries(entries)));
  }

  toggleDeleted(show: boolean): void {
    this.showDeleted.set(show);
    this.load(true);
  }

  // ── Editor ────────────────────────────────────────────────────────────────

  openEditor(calendar: WorkCalendar | null, trigger: EventTarget | null): void {
    this.editorTrigger = trigger instanceof HTMLElement ? trigger : null;
    this.actionError.set(null);
    this.editor.set({ calendar });
  }

  canEditInEditor(calendar: WorkCalendar | null): boolean {
    return calendar
      ? this.permissions.hasPermission('Calendars', 'Edit')
      : this.permissions.hasPermission('Calendars', 'Create');
  }

  onEditorClosed(): void {
    this.editor.set(null);
    this.restoreFocus();
  }

  onEditorSaved(saved: WorkCalendar, wasNew: boolean): void {
    this.editor.set(null);
    this.toast.success(wasNew ? `Calendar "${saved.name}" created.` : `Calendar "${saved.name}" saved.`);
    this.load(true);
    this.restoreFocus();
  }

  private restoreFocus(): void {
    const trigger = this.editorTrigger;
    this.editorTrigger = null;
    setTimeout(() => {
      if (trigger?.isConnected) trigger.focus();
      else document.querySelector<HTMLElement>('#cal-new')?.focus();
    });
  }

  // ── Row actions ───────────────────────────────────────────────────────────

  askSetDefault(calendar: WorkCalendar, trigger: EventTarget | null): void {
    this.confirmTrigger = trigger instanceof HTMLElement ? trigger : null;
    this.pending.set({ kind: 'default', calendar });
    this.confirmDialog().open();
  }

  askDelete(calendar: WorkCalendar, trigger: EventTarget | null): void {
    this.confirmTrigger = trigger instanceof HTMLElement ? trigger : null;
    this.pending.set({ kind: 'delete', calendar });
    this.confirmDialog().open();
  }

  onConfirmed(ok: boolean): void {
    const p = this.pending();
    this.pending.set(null);
    // The Bootstrap modal is opened from code, so it does not hand focus back itself.
    const trigger = this.confirmTrigger;
    this.confirmTrigger = null;
    setTimeout(() => {
      if (trigger?.isConnected && !trigger.hasAttribute('disabled')) trigger.focus();
      else document.querySelector<HTMLElement>('#cal-new, .cal-actions button')?.focus();
    }, 350);
    if (!ok || !p) return;
    if (p.kind === 'default') this.setDefault(p.calendar);
    else this.delete(p.calendar);
  }

  private setDefault(calendar: WorkCalendar): void {
    this.runAction(calendar, this.service.setDefault(calendar.id).pipe(map(() => undefined)),
      `"${calendar.name}" is now the default calendar.`, 'The default calendar could not be changed.');
  }

  private delete(calendar: WorkCalendar): void {
    this.runAction(calendar, this.service.delete(calendar.id),
      `Calendar "${calendar.name}" deleted.`, 'The calendar could not be deleted.');
  }

  restore(calendar: WorkCalendar): void {
    this.runAction(calendar, this.service.restore(calendar.id),
      `Calendar "${calendar.name}" restored.`, 'The calendar could not be restored.');
  }

  private runAction(
    calendar: WorkCalendar,
    request: ReturnType<WorkCalendarService['delete']>,
    successMessage: string,
    failureMessage: string,
  ): void {
    this.busyId.set(calendar.id);
    this.actionError.set(null);
    request.subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success(successMessage);
        this.load(true);
      },
      error: err => {
        this.busyId.set(null);
        // The reason (default / in use / name taken) is shown on the page, not as a toast.
        this.actionError.set(showErrorHere(err) ?? apiErrorMessage(err, failureMessage));
      },
    });
  }

  // ── Display helpers ───────────────────────────────────────────────────────

  usageOf(calendar: WorkCalendar): UsageState {
    return this.usage()[calendar.id];
  }

  /** Why the calendar cannot be deleted, or null when it can. */
  deleteBlockedReason(calendar: WorkCalendar): string | null {
    if (calendar.isDefault) return 'The default calendar cannot be deleted. Make another calendar the default first.';
    const used = this.usageOf(calendar)?.totalWorkspaceCount ?? 0;
    return used > 0
      ? `Used by ${used} workspace${used === 1 ? '' : 's'}. Move them to another calendar before deleting it.`
      : null;
  }

  defaultBlockedReason(calendar: WorkCalendar): string | null {
    return calendar.isActive ? null : 'An inactive calendar cannot be the default. Activate it first.';
  }

  readonly workingDays = summarizeWorkingDays;
  readonly hours       = summarizeHours;
  readonly hoursTitle  = hoursByDay;
  readonly dateRange   = formatDateRange;

  nextExceptionOf(calendar: WorkCalendar) {
    return nextException(calendar.exceptions, this.today);
  }

  trackCalendar = (_: number, c: WorkCalendar) => c.id;
}
