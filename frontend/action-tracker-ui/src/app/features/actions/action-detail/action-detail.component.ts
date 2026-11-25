import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, computed, HostListener, Injector, afterNextRender,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ActionItemService }     from '../../../core/services/action-item.service';
import { ToastService }          from '../../../core/services/toast.service';
import { AuthService }           from '../../../core/services/auth.service';
import { WorkflowService }       from '../../../services/workflow.service';
import { WorkspaceService }      from '../../workspaces/services/workspace.service';

import {
  ActionItem, ActionItemUpdate, ActionStatus, ActionPriority,
  AssignableUser, EscalationInfo,
} from '../../../core/models/action-item.model';
import {
  WorkflowRequest,
  ReviewWorkflowRequest,
  WORKFLOW_STATUS_CONFIG,
  WORKFLOW_TYPE_LABELS,
} from '../../../models/workflow.model';

import { DocumentsSectionComponent } from '../../../shared/components/documents-section/documents-section.component';
import { BreadcrumbComponent }       from '../../../shared/components/breadcrumb/breadcrumb.component';
import { WorkflowReviewDialogComponent } from '../../../shared/components/workflow-review-dialog/workflow-review-dialog.component';
import { WorkingDayHintComponent } from '../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../shared/components/working-days/working-days.component';
import { nonWorkingDatesMessage } from '../../../shared/validators/working-day.validator';
import { injectWorkspaceCalendar } from '../../../services/workspace-calendar.service';
import { HasPermissionDirective } from '../../../shared';
import { ActualDatesFieldsComponent } from '../../../shared/components/actual-dates-fields/actual-dates-fields.component';
import { SubItemsSectionComponent } from './sub-items-section/sub-items-section.component';
import { ActionUpdatesSectionComponent } from './action-updates-section/action-updates-section.component';
import {
  actualDatesPatch, actualDatesProblem, actualEndEditableFor, todayInTimeZone, toDateInput,
  varianceLabel, varianceTone,
} from '../../../shared/utils/actual-dates.util';

interface EditFormData {
  title: string;
  description: string;
  assigneeIds: string[];
  priority: ActionPriority;
  status: ActionStatus;
  startDate: string;
  dueDate: string;
  actualStartDate: string;
  actualEndDate: string;
  progress: number;
  isEscalated: boolean;
  escalationExplanation: string;
}

@Component({
  selector: 'app-action-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterLink, FormsModule,
    DocumentsSectionComponent,
    BreadcrumbComponent, WorkflowReviewDialogComponent,
    WorkingDayHintComponent, WorkingDaysComponent, HasPermissionDirective, ActualDatesFieldsComponent,
    SubItemsSectionComponent, ActionUpdatesSectionComponent,
  ],
  templateUrl: './action-detail.component.html',
  styleUrl:    './action-detail.component.scss',
})
export class ActionDetailComponent implements OnInit {
  private readonly route        = inject(ActivatedRoute);
  private readonly actionSvc    = inject(ActionItemService);
  private readonly toastSvc     = inject(ToastService);
  private readonly authService  = inject(AuthService);
  private readonly workflowSvc  = inject(WorkflowService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly injector     = inject(Injector);

  readonly item       = signal<ActionItem | null>(null);
  readonly loading    = signal(true);

  // ── Workflow state ──────────────────────────────────
  readonly workflowRequests   = signal<WorkflowRequest[]>([]);
  readonly pendingRequests    = signal<WorkflowRequest[]>([]);
  readonly canReviewWorkflow  = signal(false);
  readonly isCreator          = signal(false);
  readonly showDirectionForm  = signal(false);
  directionText = '';
  readonly showReviewDialog   = signal(false);
  readonly selectedRequest    = signal<WorkflowRequest | null>(null);
  readonly showWorkflowHistory = signal(false);
  readonly showEscalateDialog = signal(false);
  readonly escalating         = signal(false);
  escalateReason = '';

  readonly WORKFLOW_STATUS_CONFIG = WORKFLOW_STATUS_CONFIG;
  readonly WORKFLOW_TYPE_LABELS   = WORKFLOW_TYPE_LABELS;

  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;

  readonly workspaceId = computed(() => this.item()?.workspaceId ?? '');

  // ── Working calendar (WO-2026-11 C-3) ─────────────────
  readonly calendar = injectWorkspaceCalendar(() => this.workspaceId());
  /** Dates the item holds now; unchanged dates are never flagged (Q8). */
  readonly originalStartDate = computed(() => this.item()?.startDate?.slice(0, 10) ?? null);
  readonly originalDueDate   = computed(() => this.item()?.dueDate?.slice(0, 10) ?? null);

  // ── Actual dates (WO-2026-11 A-1) ─────────────────────
  /** Today in the workspace calendar's time zone: the latest day an actual date may be. */
  readonly today = computed(() => todayInTimeZone(this.calendar()?.calendar.timeZoneId));
  readonly varianceLabel = varianceLabel;
  readonly varianceTone  = varianceTone;
  /** Inline editor for a Done item's actual dates (a Done item accepts nothing else). */
  readonly showActualDatesForm = signal(false);
  readonly savingActualDates   = signal(false);
  actualDatesForm = { start: '', end: '' };

  readonly breadcrumbItems = computed(() => {
    const ai = this.item();
    const items: { label: string; route?: string }[] = [
      { label: 'Workspaces', route: '/workspaces' },
    ];
    if (ai) {
      items.push({ label: ai.workspaceTitle || '…', route: '/workspaces/' + ai.workspaceId });
      if (ai.projectId) {
        items.push({ label: ai.projectName || '…', route: '/projects/' + ai.projectId });
      }
      if (ai.milestoneId && ai.projectId) {
        items.push({ label: ai.milestoneName || '…', route: '/projects/' + ai.projectId + '/milestones/' + ai.milestoneId });
      }
      items.push({ label: ai.actionId });
    } else {
      items.push({ label: 'Loading…' });
    }
    return items;
  });

  readonly backRoute = computed(() => {
    const ai = this.item();
    if (ai?.milestoneId && ai?.projectId) {
      return ['/projects', ai.projectId, 'milestones', ai.milestoneId];
    }
    return ['/workspaces', ai?.workspaceId ?? ''];
  });

  readonly backLabel = computed(() => {
    const ai = this.item();
    if (ai?.milestoneId) return 'Back to Milestone';
    return 'Back to Workspace';
  });

  // ── Edit form state ──────────────────────────────────
  showEditForm = false;
  saving = false;
  assigneeDropdownOpen = false;
  assigneeSearchTerm = '';
  allUsers: AssignableUser[] = [];
  editingEscalations: EscalationInfo[] = [];
  private originalEscalated = false;
  private originalEscalationText = '';

  editForm: EditFormData = this.emptyEditForm();

  readonly STATUS_OPTIONS = [
    { value: ActionStatus.ToDo,       label: 'To Do'       },
    { value: ActionStatus.InProgress, label: 'In Progress' },
    { value: ActionStatus.InReview,   label: 'In Review'   },
    { value: ActionStatus.Done,       label: 'Done'        },
    { value: ActionStatus.Overdue,    label: 'Overdue'     },
  ];

  readonly PRIORITY_OPTIONS = [
    { value: ActionPriority.Low,      label: 'Low'      },
    { value: ActionPriority.Medium,   label: 'Medium'   },
    { value: ActionPriority.High,     label: 'High'     },
    { value: ActionPriority.Critical, label: 'Critical' },
  ];

  private readonly STATUS_MAP: Record<string, ActionStatus> = {
    todo: ActionStatus.ToDo, inprogress: ActionStatus.InProgress,
    inreview: ActionStatus.InReview, done: ActionStatus.Done, overdue: ActionStatus.Overdue,
  };
  private readonly PRIORITY_MAP: Record<string, ActionPriority> = {
    low: ActionPriority.Low, medium: ActionPriority.Medium,
    high: ActionPriority.High, critical: ActionPriority.Critical,
  };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadItem(id);
    this.loadWorkflowData(id);
  }

  // ── Data loading ───────────────────────────────────────
  private loadItem(id: string): void {
    this.loading.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        this.item.set(r.data);
        this.loading.set(false);
        this.loadUsers(r.data?.workspaceId ?? '');
      },
      error: () => {
        this.toastSvc.error('Failed to load action item.');
        this.loading.set(false);
      },
    });
  }

  private loadUsers(workspaceId: string): void {
    if (!workspaceId) {
      this.allUsers = [];
      return;
    }
    this.workspaceSvc.getAssignableUsers(workspaceId).subscribe({
      next: r => {
        const list = [...(r.data ?? [])];
        // Keep currently-assigned users visible even if they are no longer
        // part of the workspace's admins/members/PMs lists.
        const ai = this.item();
        if (ai) {
          const knownIds = new Set(list.map(u => u.userId));
          for (const a of ai.assignees) {
            if (!knownIds.has(a.userId)) {
              list.push({ userId: a.userId, userName: a.fullName, email: a.email, sources: [] });
            }
          }
        }
        this.allUsers = list;
      },
      error: () => { this.allUsers = []; },
    });
  }

  // ── Edit form ──────────────────────────────────────────
  private emptyEditForm(): EditFormData {
    return {
      title: '', description: '', assigneeIds: [],
      priority: ActionPriority.Medium, status: ActionStatus.ToDo,
      startDate: '', dueDate: '', actualStartDate: '', actualEndDate: '', progress: 0,
      isEscalated: false, escalationExplanation: '',
    };
  }

  openEditForm(): void {
    const ai = this.item();
    if (!ai) return;

    this.editingEscalations = [...(ai.escalations ?? [])]
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());

    let latestExplanation = '';
    if (ai.isEscalated && this.editingEscalations.length > 0) {
      latestExplanation = this.editingEscalations[this.editingEscalations.length - 1].explanation ?? '';
    }

    this.editForm = {
      title:       ai.title,
      description: ai.description,
      assigneeIds: ai.assignees.map(a => a.userId),
      priority:    this.resolvePriority(ai.priority),
      status:      this.resolveStatus(ai.status),
      startDate:   ai.startDate ? ai.startDate.slice(0, 10) : '',
      dueDate:     ai.dueDate.slice(0, 10),
      actualStartDate: toDateInput(ai.actualStartDate),
      actualEndDate:   toDateInput(ai.actualEndDate),
      progress:    ai.progress,
      isEscalated: !!ai.isEscalated,
      escalationExplanation: latestExplanation,
    };

    this.originalEscalated = !!ai.isEscalated;
    this.originalEscalationText = latestExplanation;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
    this.showEditForm = true;
  }

  cancelEditForm(): void {
    this.showEditForm = false;
    this.assigneeDropdownOpen = false;
    this.assigneeSearchTerm = '';
  }

  onStatusChange(): void {
    if (+this.editForm.status === ActionStatus.Done) {
      this.editForm.progress = 100;
    }
  }

  /**
   * True when this save can set the actual end: the item moves to Done here
   * and the move applies directly. A standalone item's move to Done goes for
   * approval, and the actual end is recorded when it is approved.
   */
  actualEndEditable(): boolean {
    return actualEndEditableFor(this.editForm.status, this.item()?.isStandalone);
  }

  editActualDatesProblem(): string | null {
    const endAllowed = this.actualEndEditable();
    return actualDatesProblem({
      start: this.editForm.actualStartDate,
      end: endAllowed ? this.editForm.actualEndDate : '',
      doneAfterSave: endAllowed,
      today: this.today(),
    });
  }

  saveEdit(): void {
    const ai = this.item();
    if (!ai) return;
    if (!this.editForm.title.trim() || this.editForm.assigneeIds.length === 0 || !this.editForm.dueDate) return;
    if (this.editForm.isEscalated && !this.editForm.escalationExplanation?.trim()) return;

    // Working-day rule — the server applies the same check and stays the authority.
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Planned start', value: this.editForm.startDate, original: this.originalStartDate() },
      { field: 'Planned end',   value: this.editForm.dueDate,   original: this.originalDueDate() },
    ]);
    if (dateProblem) {
      this.toastSvc.error(dateProblem);
      return;
    }
    const actualProblem = this.editActualDatesProblem();
    if (actualProblem) {
      this.toastSvc.error(actualProblem);
      return;
    }

    this.saving = true;

    const escalatedChanged = this.editForm.isEscalated !== this.originalEscalated;
    const explanationChanged = this.editForm.escalationExplanation?.trim() !== this.originalEscalationText.trim();
    const shouldSendEscalation = escalatedChanged || explanationChanged;

    const payload: ActionItemUpdate = {
      title:       this.editForm.title.trim(),
      description: this.editForm.description?.trim() ?? '',
      assigneeIds: this.editForm.assigneeIds,
      priority:    +this.editForm.priority as ActionPriority,
      status:      +this.editForm.status as ActionStatus,
      startDate:   this.editForm.startDate || null,
      dueDate:     this.editForm.dueDate,
      progress:    +this.editForm.progress,
      isEscalated: !!this.editForm.isEscalated,
      escalationExplanation: (this.editForm.isEscalated && shouldSendEscalation)
        ? this.editForm.escalationExplanation?.trim()
        : undefined,
      ...actualDatesPatch(
        { start: ai.actualStartDate, end: ai.actualEndDate },
        { start: this.editForm.actualStartDate, end: this.editForm.actualEndDate },
        this.actualEndEditable()),
    };

    this.actionSvc.update(ai.id, payload).subscribe({
      next: () => {
        this.saving = false;
        this.showEditForm = false;
        this.toastSvc.success('Action item updated.');
        this.loadItem(ai.id);
      },
      error: (err) => {
        this.saving = false;
        this.toastSvc.error(err?.error?.message ?? 'Failed to update action item.');
      },
    });
  }

  // ── Actual dates of a Done item (WO-2026-11 A-1) ───────
  openActualDatesForm(): void {
    const ai = this.item();
    if (!ai) return;
    this.actualDatesForm = { start: toDateInput(ai.actualStartDate), end: toDateInput(ai.actualEndDate) };
    this.showActualDatesForm.set(true);
    afterNextRender(() => document.getElementById('ad-done-actual-start')?.focus(), { injector: this.injector });
  }

  cancelActualDatesForm(): void {
    this.showActualDatesForm.set(false);
    afterNextRender(() => document.getElementById('ad-edit-actual-dates')?.focus(), { injector: this.injector });
  }

  doneActualDatesProblem(): string | null {
    return actualDatesProblem({
      start: this.actualDatesForm.start, end: this.actualDatesForm.end, doneAfterSave: true, today: this.today(),
    });
  }

  saveActualDates(): void {
    const ai = this.item();
    if (!ai) return;
    const problem = this.doneActualDatesProblem();
    if (problem) {
      this.toastSvc.error(problem);
      return;
    }
    // Only the actual dates: a Done item refuses any other field.
    const patch = actualDatesPatch({ start: ai.actualStartDate, end: ai.actualEndDate }, this.actualDatesForm, true);
    if (Object.keys(patch).length === 0) {
      this.cancelActualDatesForm();
      return;
    }

    this.savingActualDates.set(true);
    this.actionSvc.update(ai.id, patch).subscribe({
      next: r => {
        this.savingActualDates.set(false);
        this.toastSvc.success('Actual dates updated.');
        // The response is the updated item: no reload, so the page does not
        // flash its loading state and focus can return to the button.
        if (r?.data) this.item.set(r.data);
        else this.loadItem(ai.id);
        this.cancelActualDatesForm();   // closes the editor and returns focus to its button
      },
      error: (err) => {
        this.savingActualDates.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to update the actual dates.');
      },
    });
  }

  // ── Assignee helpers ───────────────────────────────────
  @HostListener('document:click')
  onDocumentClick(): void {
    this.assigneeDropdownOpen = false;
  }

  get filteredUsers(): AssignableUser[] {
    if (!this.assigneeSearchTerm.trim()) return this.allUsers;
    const term = this.assigneeSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.userName.toLowerCase().includes(term));
  }

  getAssigneeName(userId: string): string {
    return this.allUsers.find(u => u.userId === userId)?.userName ?? userId;
  }

  toggleAssignee(userId: string): void {
    const idx = this.editForm.assigneeIds.indexOf(userId);
    if (idx >= 0) {
      this.editForm.assigneeIds.splice(idx, 1);
    } else {
      this.editForm.assigneeIds.push(userId);
    }
  }

  isAssigneeSelected(userId: string): boolean {
    return this.editForm.assigneeIds.includes(userId);
  }

  get latestEscalation(): EscalationInfo | null {
    if (this.editingEscalations.length === 0) return null;
    return this.editingEscalations[this.editingEscalations.length - 1];
  }

  // ── Workflow methods ────────────────────────────────────
  private loadWorkflowData(actionItemId: string): void {
    this.workflowSvc.getRequestsForActionItem(actionItemId).subscribe({
      next: r => {
        const all = r.data ?? [];
        this.workflowRequests.set(all);
        this.pendingRequests.set(all.filter(req => req.status === 'Pending'));
      },
      error: () => {},
    });
    this.workflowSvc.canReview(actionItemId).subscribe({
      next: r => this.canReviewWorkflow.set(r.data?.canReview ?? false),
      error: () => {},
    });
    // Check if current user is the creator (via auth)
    this.authService.currentUser$.subscribe(user => {
      const ai = this.item();
      // We check creator by matching createdByUserId if available, otherwise skip
      if (user && ai) {
        // isCreator is used for escalation direction panel
        this.isCreator.set(true); // Default; backend enforces actual check
      }
    });
  }

  openReviewDialog(request: WorkflowRequest): void {
    this.selectedRequest.set(request);
    this.showReviewDialog.set(true);
  }

  onWorkflowReviewed(review: ReviewWorkflowRequest): void {
    const req = this.selectedRequest();
    if (!req) return;

    this.workflowSvc.reviewRequest(req.id, review).subscribe({
      next: () => {
        this.toastSvc.success(review.isApproved ? 'Request approved.' : 'Request rejected.');
        this.showReviewDialog.set(false);
        this.selectedRequest.set(null);
        const id = this.route.snapshot.paramMap.get('id')!;
        this.loadWorkflowData(id);
        this.loadItem(id);
      },
      error: err => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit review.');
        this.showReviewDialog.set(false);
      },
    });
  }

  onReviewDialogClosed(): void {
    this.showReviewDialog.set(false);
    this.selectedRequest.set(null);
  }

  submitDirection(): void {
    const ai = this.item();
    if (!ai || !this.directionText.trim()) return;

    this.workflowSvc.giveDirection({
      actionItemId: ai.id,
      directionText: this.directionText.trim(),
    }).subscribe({
      next: () => {
        this.toastSvc.success('Direction submitted.');
        this.directionText = '';
        this.showDirectionForm.set(false);
        const id = this.route.snapshot.paramMap.get('id')!;
        this.loadItem(id);
      },
      error: err => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit direction.');
      },
    });
  }

  submitEscalation(): void {
    const ai = this.item();
    if (!ai || !this.escalateReason.trim()) return;

    this.escalating.set(true);
    this.workflowSvc.escalate(ai.id, this.escalateReason.trim()).subscribe({
      next: () => {
        this.toastSvc.success('Action item escalated. Creator and manager(s) have been notified.');
        this.escalateReason = '';
        this.showEscalateDialog.set(false);
        this.escalating.set(false);
        const id = this.route.snapshot.paramMap.get('id')!;
        this.loadItem(id);
      },
      error: err => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to escalate action item.');
        this.escalating.set(false);
      },
    });
  }

  // ── Display helpers ────────────────────────────────────
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

  dueDateClass(item: ActionItem): string {
    if (item.isOverdue || item.status === ActionStatus.Overdue) return 'text-danger fw-semibold';
    if (item.daysUntilDue <= 3) return 'text-warning fw-semibold';
    return '';
  }

  private resolveStatus(val: unknown): ActionStatus {
    if (typeof val === 'number') return val;
    return this.STATUS_MAP[String(val).toLowerCase()] ?? ActionStatus.ToDo;
  }

  private resolvePriority(val: unknown): ActionPriority {
    if (typeof val === 'number') return val;
    return this.PRIORITY_MAP[String(val).toLowerCase()] ?? ActionPriority.Medium;
  }
}
