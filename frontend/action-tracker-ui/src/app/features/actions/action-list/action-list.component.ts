import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef,
  inject, signal, computed, ViewChild, HostListener,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormControl } from '@angular/forms';
import { DatePipe } from '@angular/common';
import * as XLSX from 'xlsx';
import { Subject, debounceTime, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { ToastService }      from '../../../core/services/toast.service';
import { WorkspaceService }  from '../../workspaces/services/workspace.service';

import {
  ActionItem, ActionItemMyStats, ActionItemCreate,
  ActionStatus, ActionPriority, AssignableUser, EscalationInfo,
} from '../../../core/models/action-item.model';
import { PagedResult }    from '../../../core/models/api-response.model';
import { WorkspaceList }  from '../../workspaces/models/workspace.model';

import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent }    from '../../../shared/components/breadcrumb/breadcrumb.component';
import { HasPermissionDirective } from '../../../shared';

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

interface ActionItemFormData {
  workspaceId: string;
  title: string;
  description: string;
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  startDate: string;
  dueDate: string;
  progress: number;
  isEscalated: boolean;
  escalationExplanation: string;
}

@Component({
  selector: 'app-action-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule, ReactiveFormsModule, RouterLink, DatePipe,
    ConfirmDialogComponent, PageHeaderComponent, BreadcrumbComponent,
    HasPermissionDirective,
  ],
  templateUrl: './action-list.component.html',
  styleUrl:    './action-list.component.scss',
})
export class ActionListComponent implements OnInit, OnDestroy {
  private readonly actionSvc    = inject(ActionItemService);
  private readonly toastSvc     = inject(ToastService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly cdr          = inject(ChangeDetectorRef);
  private readonly destroy$     = new Subject<void>();

  @ViewChild('deleteDialog') deleteDialog!: ConfirmDialogComponent;

  // ── List state ────────────────────────────────────────
  readonly items        = signal<ActionItem[]>([]);
  readonly totalCount   = signal(0);
  readonly loading         = signal(false);
  readonly myStats         = signal<ActionItemMyStats | null>(null);
  readonly statsLoading    = signal(false);
  readonly showDeleted     = signal(false);
  readonly pendingDeleteId = signal<string | null>(null);
  readonly exporting   = signal(false);
  readonly printingPdf = signal(false);

  // ── Filters ───────────────────────────────────────────
  readonly searchCtrl     = new FormControl<string>('');
  readonly filterStatus   = signal<ActionStatus   | null>(null);
  readonly filterPriority = signal<ActionPriority | null>(null);
  readonly pageNumber     = signal(1);
  readonly pageSize       = signal(10);
  readonly sortBy         = signal('dueDate');
  readonly sortDesc       = signal(false);

  readonly hasActiveFilter = computed(() =>
    !!(this.searchCtrl.value?.trim() ||
       this.filterStatus()   !== null ||
       this.filterPriority() !== null)
  );

  // ── Pagination ────────────────────────────────────────
  readonly totalPages  = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  readonly showingFrom = computed(() => (this.pageNumber() - 1) * this.pageSize() + 1);
  readonly showingTo   = computed(() => Math.min(this.pageNumber() * this.pageSize(), this.totalCount()));
  readonly skeletonRows = Array.from({ length: 8 });

  // ── Offcanvas form state ──────────────────────────────
  showActionForm    = false;
  editingActionId: string | null = null;
  actionSaving      = false;
  actionForm: ActionItemFormData = this.emptyActionForm();
  assigneeDropdownOpen  = false;
  assigneeSearchTerm    = '';
  editingEscalations: EscalationInfo[] = [];
  private originalEscalated     = false;
  private originalEscalationText = '';

  allUsers:      AssignableUser[] = [];
  allWorkspaces: WorkspaceList[]  = [];

  // ── Expose constants to template ──────────────────────
  readonly ActionStatus     = ActionStatus;
  readonly ActionPriority   = ActionPriority;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;
  readonly PAGE_SIZE_OPTIONS = [10, 20, 50];

  // ── Claim maps (same as workspace-detail) ─────────────
  private readonly STATUS_MAP: Record<string, ActionStatus> = {
    todo: ActionStatus.ToDo, inprogress: ActionStatus.InProgress,
    inreview: ActionStatus.InReview, done: ActionStatus.Done,
    overdue: ActionStatus.Overdue,
  };
  private readonly PRIORITY_MAP: Record<string, ActionPriority> = {
    low: ActionPriority.Low, medium: ActionPriority.Medium,
    high: ActionPriority.High, critical: ActionPriority.Critical,
  };

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    this.searchCtrl.valueChanges.pipe(
      debounceTime(300),
      takeUntil(this.destroy$),
    ).subscribe(() => {
      this.pageNumber.set(1);
      this.load();
    });

    this.loadStats();
    this.load();
    this.loadAllUsers();
    this.loadAllWorkspaces();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Data loading ──────────────────────────────────────
  loadStats(): void {
    this.statsLoading.set(true);
    this.actionSvc.getMyStats().subscribe({
      next: r => { this.myStats.set(r.data ?? null); this.statsLoading.set(false); },
      error: () => this.statsLoading.set(false),
    });
  }

  load(): void {
    this.loading.set(true);
    const filter = {
      pageNumber:     this.pageNumber(),
      pageSize:       this.pageSize(),
      sortBy:         this.sortBy(),
      sortDescending: this.sortDesc(),
      searchTerm:     this.searchCtrl.value?.trim() || undefined,
      status:         this.filterStatus()   ?? undefined,
      priority:       this.filterPriority() ?? undefined,
      includeDeleted: this.showDeleted() || undefined,
    };

    this.actionSvc.getMyActions(filter).subscribe({
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

  private loadAllUsers(): void {
    this.actionSvc.getAssignableUsers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => { this.allUsers = r.data ?? []; this.cdr.markForCheck(); } });
  }

  private loadAllWorkspaces(): void {
    this.workspaceSvc.getWorkspaces()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => { this.allWorkspaces = r.data ?? []; this.cdr.markForCheck(); } });
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

  toggleShowDeleted(): void {
    this.showDeleted.update(v => !v);
    this.pageNumber.set(1);
    this.load();
  }

  // ── Offcanvas form ────────────────────────────────────
  private emptyActionForm(): ActionItemFormData {
    return {
      workspaceId: '',
      title: '',
      description: '',
      assigneeIds: [],
      priority: ActionPriority.Medium,
      status: ActionStatus.ToDo,
      startDate: '',
      dueDate: '',
      progress: 0,
      isEscalated: false,
      escalationExplanation: '',
    };
  }

  private resolveStatus(val: unknown): ActionStatus {
    if (typeof val === 'number') return val;
    return this.STATUS_MAP[String(val).toLowerCase()] ?? ActionStatus.ToDo;
  }

  private resolvePriority(val: unknown): ActionPriority {
    if (typeof val === 'number') return val;
    return this.PRIORITY_MAP[String(val).toLowerCase()] ?? ActionPriority.Medium;
  }

  onStatusChange(): void {
    if (+this.actionForm.status === ActionStatus.Done) {
      this.actionForm.progress = 100;
    }
  }

  openNewActionForm(): void {
    this.editingActionId = null;
    this.actionForm = this.emptyActionForm();
    this.editingEscalations = [];
    this.originalEscalated = false;
    this.originalEscalationText = '';
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
    this.cdr.markForCheck();
  }

  openEditActionForm(item: ActionItem): void {
    this.editingActionId = item.id;
    this.editingEscalations = [...(item.escalations ?? [])]
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());

    let latestExplanation = '';
    if (item.isEscalated && this.editingEscalations.length > 0) {
      latestExplanation = this.editingEscalations[this.editingEscalations.length - 1].explanation ?? '';
    }

    this.actionForm = {
      workspaceId: (item as any).workspaceId ?? '',
      title:       item.title,
      description: item.description,
      assigneeIds: item.assignees.map(a => a.userId),
      priority:    this.resolvePriority(item.priority),
      status:      this.resolveStatus(item.status),
      startDate:   item.startDate ? item.startDate.slice(0, 10) : '',
      dueDate:     item.dueDate.slice(0, 10),
      progress:    item.progress,
      isEscalated: !!item.isEscalated,
      escalationExplanation: latestExplanation,
    };

    this.originalEscalated = !!item.isEscalated;
    this.originalEscalationText = latestExplanation;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showActionForm = true;
    this.cdr.markForCheck();
  }

  cancelActionForm(): void {
    this.showActionForm = false;
    this.editingActionId = null;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.cdr.markForCheck();
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.assigneeDropdownOpen) {
      this.assigneeDropdownOpen = false;
      this.cdr.markForCheck();
    }
  }

  get latestEscalation(): EscalationInfo | null {
    return this.editingEscalations.length > 0
      ? this.editingEscalations[this.editingEscalations.length - 1]
      : null;
  }

  get filteredUsers(): AssignableUser[] {
    if (!this.assigneeSearchTerm.trim()) return this.allUsers;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  getAssigneeName(userId: string): string {
    return this.allUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  toggleAssignee(userId: string): void {
    const idx = this.actionForm.assigneeIds.indexOf(userId);
    if (idx >= 0) this.actionForm.assigneeIds.splice(idx, 1);
    else          this.actionForm.assigneeIds.push(userId);
  }

  isAssigneeSelected(userId: string): boolean {
    return this.actionForm.assigneeIds.includes(userId);
  }

  saveAction(): void {
    if (!this.actionForm.title.trim() || !this.actionForm.workspaceId ||
        this.actionForm.assigneeIds.length === 0 || !this.actionForm.dueDate) return;
    if (this.actionForm.isEscalated && !this.actionForm.escalationExplanation?.trim()) return;

    this.actionSaving = true;
    this.cdr.markForCheck();

    const escalatedChanged    = this.actionForm.isEscalated !== this.originalEscalated;
    const explanationChanged  = this.actionForm.escalationExplanation?.trim() !== this.originalEscalationText.trim();
    const shouldSendEscalation = escalatedChanged || explanationChanged;

    const payload: ActionItemCreate = {
      title:       this.actionForm.title.trim(),
      description: this.actionForm.description?.trim() ?? '',
      workspaceId: this.actionForm.workspaceId,
      isStandalone: true,
      assigneeIds: this.actionForm.assigneeIds,
      priority:    +this.actionForm.priority as ActionPriority,
      status:      +this.actionForm.status as ActionStatus,
      startDate:   this.actionForm.startDate || null,
      dueDate:     this.actionForm.dueDate,
      progress:    +this.actionForm.progress,
      isEscalated: !!this.actionForm.isEscalated,
      escalationExplanation: (this.actionForm.isEscalated && shouldSendEscalation)
        ? this.actionForm.escalationExplanation?.trim()
        : undefined,
    };

    const obs$ = this.editingActionId
      ? this.actionSvc.update(this.editingActionId, payload)
      : this.actionSvc.create(payload);

    obs$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.actionSaving = false;
        this.showActionForm = false;
        this.editingActionId = null;
        this.toastSvc.success(this.editingActionId ? 'Action item updated.' : 'Action item created.');
        this.load();
        this.loadStats();
        this.cdr.markForCheck();
      },
      error: err => {
        this.actionSaving = false;
        this.toastSvc.error(err?.error?.message ?? 'Failed to save action item.');
        this.cdr.markForCheck();
      },
    });
  }

  // ── Delete ────────────────────────────────────────────
  confirmDelete(id: string): void {
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
        this.loadStats();
      },
      error: () => this.toastSvc.error('Failed to delete action item.'),
    });
  }

  // ── Badge helpers (same as workspace-detail) ──────────
  priorityClass(p: ActionPriority): string {
    switch (+p) {
      case ActionPriority.Critical: return 'badge bg-danger';
      case ActionPriority.High:     return 'badge bg-warning text-dark';
      case ActionPriority.Medium:   return 'badge bg-info text-dark';
      case ActionPriority.Low:      return 'badge bg-secondary';
      default:                      return 'badge bg-light text-dark';
    }
  }

  statusClass(s: ActionStatus): string {
    switch (+s) {
      case ActionStatus.ToDo:       return 'badge bg-secondary';
      case ActionStatus.InProgress: return 'badge bg-primary';
      case ActionStatus.InReview:   return 'badge bg-warning text-dark';
      case ActionStatus.Done:       return 'badge bg-success';
      case ActionStatus.Overdue:    return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
  }

  // ── Due-date helpers ──────────────────────────────────
  dueDateClass(item: ActionItem): string {
    if (item.isOverdue || item.status === ActionStatus.Overdue) return 'text-danger fw-semibold';
    if (item.daysUntilDue <= 3) return 'text-warning fw-semibold';
    return '';
  }

  assigneeNames(item: ActionItem): string {
    return item.assignees?.map(a => a.fullName).join(', ') || '—';
  }

  assigneeInitial(item: ActionItem): string {
    return item.assignees?.[0]?.fullName?.charAt(0)?.toUpperCase() || '?';
  }

  // ── Export to Excel ───────────────────────────────────
  exportToExcel(): void {
    this.exporting.set(true);
    this.cdr.markForCheck();

    this.actionSvc.getAllMyActions().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        const items = r.data?.items ?? [];
        const stats = this.myStats();

        const wb = XLSX.utils.book_new();

        // ── Sheet 1: Summary ──────────────────────────
        const summaryRows = [
          ['My Action Items – Summary', ''],
          ['Generated', new Date().toLocaleString()],
          [''],
          ['Metric', 'Value'],
          ['Total Actions',      stats?.totalCount          ?? 0],
          ['Critical',           stats?.criticalCount       ?? 0],
          ['In Progress',        stats?.inProgressCount     ?? 0],
          ['Completed',          stats?.completedCount      ?? 0],
          ['Overdue',            stats?.overdueCount        ?? 0],
          ['Completion Rate',    `${stats?.completionRate   ?? 0}%`],
          ['On-Time Completion', `${stats?.onTimeCompletionRate ?? 0}%`],
        ];
        const ws1 = XLSX.utils.aoa_to_sheet(summaryRows);
        ws1['!cols'] = [{ wch: 24 }, { wch: 20 }];
        XLSX.utils.book_append_sheet(wb, ws1, 'Summary');

        // ── Sheet 2: Actions ──────────────────────────
        const header = [
          'ID', 'Title', 'Workspace', 'Project', 'Milestone',
          'Priority', 'Status', 'Assignees',
          'Start Date', 'Due Date', 'Progress (%)',
          'Escalated', 'Created', 'Updated',
        ];
        const rows = items.map(i => [
          i.actionId,
          i.title,
          i.workspaceTitle ?? '',
          i.projectName   ?? '',
          i.milestoneName ?? '',
          i.priorityLabel ?? this.priorityLabel(i.priority),
          i.statusLabel   ?? this.statusLabel(i.status),
          (i.assignees ?? []).map(a => a.fullName).join(', '),
          i.startDate ? i.startDate.slice(0, 10) : '',
          i.dueDate.slice(0, 10),
          i.progress,
          i.isEscalated ? 'Yes' : 'No',
          i.createdAt ? new Date(i.createdAt).toLocaleDateString() : '',
          i.updatedAt ? new Date(i.updatedAt).toLocaleDateString() : '',
        ]);
        const ws2 = XLSX.utils.aoa_to_sheet([header, ...rows]);
        ws2['!cols'] = [
          { wch: 10 }, { wch: 40 }, { wch: 24 }, { wch: 20 }, { wch: 20 },
          { wch: 12 }, { wch: 14 }, { wch: 30 },
          { wch: 14 }, { wch: 14 }, { wch: 14 },
          { wch: 10 }, { wch: 16 }, { wch: 16 },
        ];
        XLSX.utils.book_append_sheet(wb, ws2, 'My Actions');

        XLSX.writeFile(wb, `my-actions-${new Date().toISOString().slice(0, 10)}.xlsx`);
        this.exporting.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.toastSvc.error('Excel export failed.');
        this.exporting.set(false);
        this.cdr.markForCheck();
      },
    });
  }

  // ── Print as PDF ──────────────────────────────────────
  printAsPdf(): void {
    this.printingPdf.set(true);
    this.cdr.markForCheck();

    this.actionSvc.getAllMyActions().pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        const items = r.data?.items ?? [];
        const stats = this.myStats();
        const now   = new Date().toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });

        const priorityBadge = (p: number): string => {
          const map: Record<number, string> = { 4: '#dc3545', 3: '#fd7e14', 2: '#0dcaf0', 1: '#6c757d' };
          const labels: Record<number, string> = { 4: 'Critical', 3: 'High', 2: 'Medium', 1: 'Low' };
          return `<span style="background:${map[p]??'#6c757d'};color:#fff;padding:2px 8px;border-radius:4px;font-size:11px;">${labels[p]??p}</span>`;
        };
        const statusBadge = (s: number): string => {
          const map: Record<number, string> = { 1: '#6c757d', 2: '#0d6efd', 3: '#ffc107', 4: '#198754', 5: '#dc3545' };
          const labels: Record<number, string> = { 1: 'To Do', 2: 'In Progress', 3: 'In Review', 4: 'Done', 5: 'Overdue' };
          const fg = s === 3 ? '#000' : '#fff';
          return `<span style="background:${map[s]??'#6c757d'};color:${fg};padding:2px 8px;border-radius:4px;font-size:11px;">${labels[s]??s}</span>`;
        };

        const rows = items.map(i => `
          <tr>
            <td>${i.actionId}</td>
            <td><strong>${i.title}</strong>${i.description ? `<br><small style="color:#666">${i.description.slice(0,80)}${i.description.length>80?'…':''}</small>` : ''}</td>
            <td>${i.workspaceTitle ?? ''}</td>
            <td>${(i.assignees ?? []).map(a => a.fullName).join(', ')}</td>
            <td>${priorityBadge(+i.priority)}</td>
            <td>${statusBadge(+i.status)}</td>
            <td style="text-align:center">${i.progress}%</td>
            <td>${i.dueDate.slice(0,10)}</td>
            <td style="text-align:center">${i.isEscalated ? '🚨' : ''}</td>
          </tr>`).join('');

        const html = `<!DOCTYPE html><html lang="en"><head>
          <meta charset="UTF-8"/>
          <title>My Action Items – ${now}</title>
          <style>
            @page { size: A4 landscape; margin: 15mm; }
            * { box-sizing: border-box; }
            body { font-family: Arial, sans-serif; font-size: 12px; color: #212529; margin: 0; }
            h1 { font-size: 20px; margin: 0 0 4px; color: #0d6efd; }
            .subtitle { color: #666; margin: 0 0 16px; font-size: 12px; }
            .stats { display: grid; grid-template-columns: repeat(7, 1fr); gap: 8px; margin-bottom: 20px; }
            .stat-card { border: 1px solid #dee2e6; border-radius: 6px; padding: 8px 12px; text-align: center; }
            .stat-card .val { font-size: 22px; font-weight: 700; color: #0d6efd; }
            .stat-card .lbl { font-size: 10px; color: #666; }
            table { width: 100%; border-collapse: collapse; font-size: 11px; }
            th { background: #0d6efd; color: #fff; padding: 6px 8px; text-align: left; }
            td { padding: 5px 8px; border-bottom: 1px solid #e9ecef; vertical-align: top; }
            tr:nth-child(even) td { background: #f8f9fa; }
            .footer { margin-top: 12px; text-align: right; font-size: 10px; color: #999; }
          </style>
        </head><body>
          <h1>My Action Items</h1>
          <p class="subtitle">Printed on ${now} &nbsp;|&nbsp; Total: ${items.length} items</p>
          <div class="stats">
            <div class="stat-card"><div class="val">${stats?.totalCount??0}</div><div class="lbl">Total</div></div>
            <div class="stat-card"><div class="val">${stats?.criticalCount??0}</div><div class="lbl">Critical</div></div>
            <div class="stat-card"><div class="val">${stats?.inProgressCount??0}</div><div class="lbl">In Progress</div></div>
            <div class="stat-card"><div class="val">${stats?.completedCount??0}</div><div class="lbl">Completed</div></div>
            <div class="stat-card"><div class="val">${stats?.overdueCount??0}</div><div class="lbl">Overdue</div></div>
            <div class="stat-card"><div class="val">${stats?.completionRate??0}%</div><div class="lbl">Completion Rate</div></div>
            <div class="stat-card"><div class="val">${stats?.onTimeCompletionRate??0}%</div><div class="lbl">On-Time Rate</div></div>
          </div>
          <table>
            <thead><tr>
              <th>ID</th><th>Title</th><th>Workspace</th><th>Assignees</th>
              <th>Priority</th><th>Status</th><th>Progress</th><th>Due Date</th><th>Esc.</th>
            </tr></thead>
            <tbody>${rows}</tbody>
          </table>
          <div class="footer">Action Tracker &nbsp;|&nbsp; ${now}</div>
          <script>window.onload=function(){window.print();window.onafterprint=function(){window.close();};}<\/script>
        </body></html>`;

        const win = window.open('', '_blank', 'width=1200,height=800');
        if (win) {
          win.document.write(html);
          win.document.close();
        }
        this.printingPdf.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.toastSvc.error('Failed to prepare print view.');
        this.printingPdf.set(false);
        this.cdr.markForCheck();
      },
    });
  }

  // ── Label helpers for export ──────────────────────────
  private priorityLabel(p: ActionPriority): string {
    return ({ [ActionPriority.Critical]: 'Critical', [ActionPriority.High]: 'High',
              [ActionPriority.Medium]: 'Medium', [ActionPriority.Low]: 'Low' } as Record<number, string>)[+p] ?? String(p);
  }

  private statusLabel(s: ActionStatus): string {
    return ({ [ActionStatus.ToDo]: 'To Do', [ActionStatus.InProgress]: 'In Progress',
              [ActionStatus.InReview]: 'In Review', [ActionStatus.Done]: 'Done',
              [ActionStatus.Overdue]: 'Overdue' } as Record<number, string>)[+s] ?? String(s);
  }

  trackById(_: number, item: ActionItem): string { return item.id; }
}
