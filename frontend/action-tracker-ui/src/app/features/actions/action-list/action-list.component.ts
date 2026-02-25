import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy,
  inject, signal, computed, ViewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Subject, debounceTime, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { UserService }       from '../../../core/services/user.service';
import { ToastService }      from '../../../core/services/toast.service';
import { ReportService }     from '../../../core/services/report.service';

import {
  ActionItem, ActionItemFilter,
  ActionStatus, ActionPriority, ActionCategory,
} from '../../../core/models/action-item.model';
import { TeamMember }        from '../../../core/models/user.model';
import { PagedResult }       from '../../../core/models/api-response.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { ProgressBarComponent }   from '../../../shared/components/progress-bar/progress-bar.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';

export const CATEGORY_LABELS: Record<ActionCategory, string> = {
  [ActionCategory.Operations]:    'Operations',
  [ActionCategory.Strategic]:     'Strategic',
  [ActionCategory.HR]:            'HR',
  [ActionCategory.Finance]:       'Finance',
  [ActionCategory.IT]:            'IT',
  [ActionCategory.Compliance]:    'Compliance',
  [ActionCategory.Communication]: 'Communication',
};

export const STATUS_OPTIONS: { value: ActionStatus; label: string }[] = [
  { value: ActionStatus.ToDo,       label: 'To Do'       },
  { value: ActionStatus.InProgress, label: 'In Progress' },
  { value: ActionStatus.InReview,   label: 'In Review'   },
  { value: ActionStatus.Done,       label: 'Done'        },
  { value: ActionStatus.Overdue,    label: 'Overdue'     },
];

export const PRIORITY_OPTIONS: { value: ActionPriority; label: string }[] = [
  { value: ActionPriority.Critical, label: 'Critical' },
  { value: ActionPriority.High,     label: 'High'     },
  { value: ActionPriority.Medium,   label: 'Medium'   },
  { value: ActionPriority.Low,      label: 'Low'      },
];

@Component({
  selector: 'app-action-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, ReactiveFormsModule, RouterLink, DatePipe,
    StatusBadgeComponent, PriorityBadgeComponent,
    ProgressBarComponent, ConfirmDialogComponent, PageHeaderComponent,
  ],
  templateUrl: './action-list.component.html',
  styleUrl:    './action-list.component.scss',
})
export class ActionListComponent implements OnInit, OnDestroy {
  private readonly actionSvc  = inject(ActionItemService);
  private readonly userSvc    = inject(UserService);
  private readonly toastSvc   = inject(ToastService);
  private readonly reportSvc  = inject(ReportService);
  readonly router             = inject(Router);
  private readonly destroy$   = new Subject<void>();

  @ViewChild('deleteDialog') deleteDialog!: ConfirmDialogComponent;

  // ── State ─────────────────────────────────────────────
  readonly items        = signal<ActionItem[]>([]);
  readonly totalCount   = signal(0);
  readonly loading      = signal(false);
  readonly teamMembers  = signal<TeamMember[]>([]);
  readonly cardView     = signal(false);
  readonly exportingCsv = signal(false);
  readonly openStatusRowId  = signal<number | null>(null);
  readonly pendingDeleteId  = signal<number | null>(null);

  // ── Filters ───────────────────────────────────────────
  readonly searchCtrl     = new FormControl<string>('');
  readonly filterStatus   = signal<ActionStatus   | null>(null);
  readonly filterPriority = signal<ActionPriority | null>(null);
  readonly filterAssignee = signal<string | null>(null);
  readonly pageNumber     = signal(1);
  readonly pageSize       = signal(10);
  readonly sortBy         = signal('dueDate');
  readonly sortDesc       = signal(false);

  readonly hasActiveFilter = computed(() =>
    !!(this.searchCtrl.value?.trim() ||
       this.filterStatus()   !== null ||
       this.filterPriority() !== null ||
       this.filterAssignee() !== null)
  );

  // ── Derived stats ─────────────────────────────────────
  readonly statTotal      = computed(() => this.totalCount());
  readonly statCritHigh   = computed(() =>
    this.items().filter(i => i.priority === ActionPriority.Critical || i.priority === ActionPriority.High).length);
  readonly statInProgress = computed(() =>
    this.items().filter(i => i.status === ActionStatus.InProgress).length);
  readonly statDone       = computed(() =>
    this.items().filter(i => i.status === ActionStatus.Done).length);
  readonly statOverdue    = computed(() =>
    this.items().filter(i => i.isOverdue || i.status === ActionStatus.Overdue).length);

  // ── Pagination ────────────────────────────────────────
  readonly totalPages  = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  readonly showingFrom = computed(() => (this.pageNumber() - 1) * this.pageSize() + 1);
  readonly showingTo   = computed(() => Math.min(this.pageNumber() * this.pageSize(), this.totalCount()));
  readonly skeletonRows = Array.from({ length: 8 });

  // ── Expose constants to template ──────────────────────
  readonly ActionStatus     = ActionStatus;
  readonly ActionPriority   = ActionPriority;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;
  readonly CATEGORY_LABELS  = CATEGORY_LABELS;
  readonly PAGE_SIZE_OPTIONS = [10, 20, 50];

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    this.searchCtrl.valueChanges.pipe(
      debounceTime(300),
      takeUntil(this.destroy$),
    ).subscribe(() => {
      this.pageNumber.set(1);
      this.load();
    });

    this.userSvc.getTeamMembers().subscribe({
      next: r => this.teamMembers.set(r.data ?? []),
      error: () => {},
    });

    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Data loading ──────────────────────────────────────
  load(): void {
    this.loading.set(true);
    const filter: ActionItemFilter = {
      pageNumber:     this.pageNumber(),
      pageSize:       this.pageSize(),
      sortBy:         this.sortBy(),
      sortDescending: this.sortDesc(),
      searchTerm:     this.searchCtrl.value?.trim() || undefined,
      status:         this.filterStatus()   ?? undefined,
      priority:       this.filterPriority() ?? undefined,
      assigneeId:     this.filterAssignee() ?? undefined,
    };

    this.actionSvc.getAll(filter).subscribe({
      next: r => {
        const paged: PagedResult<ActionItem> = r.data;
        this.items.set(paged.items);
        this.totalCount.set(paged.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load action items.');
      },
    });
  }

  // ── Filter helpers ────────────────────────────────────
  applyFilter(): void {
    this.pageNumber.set(1);
    this.load();
  }

  clearFilters(): void {
    this.searchCtrl.setValue('', { emitEvent: false });
    this.filterStatus.set(null);
    this.filterPriority.set(null);
    this.filterAssignee.set(null);
    this.pageNumber.set(1);
    this.load();
  }

  onSortChange(key: string): void {
    if (this.sortBy() === key) {
      this.sortDesc.update(d => !d);
    } else {
      this.sortBy.set(key);
      this.sortDesc.set(false);
    }
    this.pageNumber.set(1);
    this.load();
  }

  // ── Pagination ────────────────────────────────────────
  prevPage(): void {
    if (this.pageNumber() > 1) { this.pageNumber.update(p => p - 1); this.load(); }
  }

  nextPage(): void {
    if (this.pageNumber() < this.totalPages()) { this.pageNumber.update(p => p + 1); this.load(); }
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(+size);
    this.pageNumber.set(1);
    this.load();
  }

  // ── Inline status ─────────────────────────────────────
  toggleStatusMenu(id: number, event: Event): void {
    event.stopPropagation();
    this.openStatusRowId.update(cur => cur === id ? null : id);
  }

  changeStatus(item: ActionItem, status: ActionStatus, event: Event): void {
    event.stopPropagation();
    this.openStatusRowId.set(null);
    if (item.status === status) return;

    this.actionSvc.updateStatus(item.id, status).subscribe({
      next: r => {
        this.items.update(list => list.map(i => i.id === item.id ? r.data : i));
        this.toastSvc.success('Status updated.');
      },
      error: () => this.toastSvc.error('Failed to update status.'),
    });
  }

  closeStatusMenu(): void { this.openStatusRowId.set(null); }

  // ── Delete ────────────────────────────────────────────
  confirmDelete(id: number): void {
    this.pendingDeleteId.set(id);
    this.deleteDialog.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    const id = this.pendingDeleteId();
    this.pendingDeleteId.set(null);
    if (!confirmed || id === null) return;

    this.actionSvc.delete(id).subscribe({
      next: () => {
        this.items.update(list => list.filter(i => i.id !== id));
        this.totalCount.update(c => c - 1);
        this.toastSvc.success('Action item deleted.');
      },
      error: () => this.toastSvc.error('Failed to delete action item.'),
    });
  }

  // ── CSV export ────────────────────────────────────────
  exportCsv(): void {
    this.exportingCsv.set(true);
    const filter = {
      searchTerm:     this.searchCtrl.value?.trim(),
      status:         this.filterStatus(),
      priority:       this.filterPriority(),
      assigneeId:     this.filterAssignee(),
      sortBy:         this.sortBy(),
      sortDescending: this.sortDesc(),
    };

    this.reportSvc.exportCsv(filter).subscribe({
      next: blob => {
        this.exportingCsv.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `action-items-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.exportingCsv.set(false);
        this.toastSvc.error('Export failed. Please try again.');
      },
    });
  }

  // ── Due-date helpers ──────────────────────────────────
  dueDateClass(item: ActionItem): string {
    if (item.isOverdue || item.status === ActionStatus.Overdue) return 'due--overdue';
    if (item.daysUntilDue <= 3) return 'due--warning';
    return 'due--ok';
  }

  dueDateLabel(item: ActionItem): string {
    if (item.isOverdue) return `${Math.abs(item.daysUntilDue)}d overdue`;
    if (item.daysUntilDue === 0) return 'Due today';
    if (item.daysUntilDue === 1) return 'Due tomorrow';
    return `${item.daysUntilDue}d left`;
  }

  toggleCardView(): void { this.cardView.update(v => !v); }

  trackById(_: number, item: ActionItem): number { return item.id; }
}
