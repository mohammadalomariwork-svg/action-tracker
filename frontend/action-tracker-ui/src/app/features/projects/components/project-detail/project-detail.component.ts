import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import * as XLSX from 'xlsx';

import { ProjectService } from '../../services/project.service';
import { MilestoneService } from '../../services/milestone.service';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ProjectWorkflowService } from '../../../../services/project-workflow.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  ProjectResponse, ProjectUpdate, ProjectStats,
  ProjectType, ProjectStatus, ProjectPriority,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { ProjectApprovalRequest } from '../../models/project-approval.models';
import { MilestoneResponse } from '../../models/milestone.models';
import {
  ActionItem, ActionItemFilter, ActionStatus, AssignableUser,
} from '../../../../core/models/action-item.model';
import { PagedResult } from '../../../../core/models/api-response.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import { MilestoneSectionComponent } from '../milestone-section/milestone-section.component';
import { RiskRegisterSectionComponent } from '../../risk-register-section/risk-register-section.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';

interface EditFormData {
  name: string;
  description: string;
  projectType: ProjectType;
  strategicObjectiveId: string | null;
  priority: ProjectPriority;
  projectManagerUserId: string;
  sponsorUserIds: string[];
  plannedStartDate: string;
  plannedEndDate: string;
  approvedBudget: number | null;
  status: ProjectStatus;
  actualStartDate: string;
}

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink, FormsModule,
    CommentsSectionComponent, DocumentsSectionComponent,
    MilestoneSectionComponent, RiskRegisterSectionComponent, BreadcrumbComponent, HasPermissionDirective,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectService     = inject(ProjectService);
  private readonly milestoneService   = inject(MilestoneService);
  private readonly actionService      = inject(ActionItemService);
  private readonly workflowService    = inject(ProjectWorkflowService);
  private readonly authService        = inject(AuthService);
  private readonly route              = inject(ActivatedRoute);
  private readonly destroyRef         = inject(DestroyRef);
  private readonly toastSvc           = inject(ToastService);

  projectId!: string;
  project: ProjectResponse | null = null;
  stats: ProjectStats | null = null;
  isLoading = false;
  errorMessage: string | null = null;

  // ── Gantt ───────────────────────────────────────────────
  showGantt      = false;
  ganttLoading   = false;
  ganttLoaded    = false;
  ganttMilestones: MilestoneResponse[] = [];
  ganttActions:    ActionItem[]        = [];

  // Gantt tooltip
  ganttTooltip: {
    type: 'project' | 'milestone' | 'action';
    title: string;
    status: string;
    startDate: string | null;
    endDate: string | null;
    progress?: number;
    priority?: string;
    assignees?: string[];
    x: number;
    y: number;
  } | null = null;

  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  // ── Edit form state ──────────────────────────────────
  showEditForm    = false;
  saving          = false;
  saveEditError: string | null = null;
  allUsers: AssignableUser[] = [];
  strategicObjectives: StrategicObjectiveOption[] = [];
  strategicObjectivesLoaded = false;
  sponsorDropdownOpen = false;
  sponsorSearchTerm = '';

  editForm: EditFormData = this.emptyEditForm();

  // ── Approval workflow ──────────────────────────────
  approvalRequests: ProjectApprovalRequest[] = [];
  canReview = false;
  currentUserId = '';
  showApprovalHistory = true;
  showSubmitModal = false;
  submitReason = '';
  submittingApproval = false;
  submitError: string | null = null;
  submitValidationErrors: string[] = [];
  validatingSubmit = false;
  showReviewModal = false;
  reviewIsApproval = true;
  reviewComment = '';
  reviewingApproval = false;
  pendingRequestId: string | null = null;

  readonly PROJECT_PRIORITY_OPTIONS = [
    { value: ProjectPriority.Low,      label: 'Low'      },
    { value: ProjectPriority.Medium,   label: 'Medium'   },
    { value: ProjectPriority.High,     label: 'High'     },
    { value: ProjectPriority.Critical, label: 'Critical' },
  ];

  readonly PROJECT_STATUS_OPTIONS = [
    { value: ProjectStatus.Draft,     label: 'Draft'     },
    { value: ProjectStatus.Active,    label: 'Active'    },
    { value: ProjectStatus.OnHold,    label: 'On Hold'   },
    { value: ProjectStatus.Completed, label: 'Completed' },
    { value: ProjectStatus.Cancelled, label: 'Cancelled' },
  ];

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.loadProject();
    this.loadStats();
    this.loadUsers();
    this.loadApprovalRequests();
    this.loadCanReview();
    this.authService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(u => {
      if (u) this.currentUserId = u.userId;
    });
  }

  // ── Data loading ───────────────────────────────────────
  private loadProject(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.projectService.getById(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.project = res.data ?? null;
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project details.';
          this.isLoading = false;
        },
      });
  }

  loadStats(): void {
    this.projectService.getStats(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.stats = res.data ?? null; },
        error: () => {},
      });
  }

  private loadUsers(): void {
    this.projectService.getAssignableUsers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.allUsers = res.data ?? []; },
        error: () => {},
      });
  }

  // ── Approval workflow methods ──────────────────────────
  loadApprovalRequests(): void {
    this.workflowService.getApprovalRequestsForProject(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          if (res.success) this.approvalRequests = res.data;
        },
        error: () => {},
      });
  }

  private loadCanReview(): void {
    this.workflowService.canReviewProject(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          if (res.success) this.canReview = res.data.canReview;
        },
        error: () => {},
      });
  }

  get canSubmitForApproval(): boolean {
    return !!this.project
      && this.project.status === ProjectStatus.Draft
      && this.project.projectManagerUserId === this.currentUserId;
  }

  get isPendingApproval(): boolean {
    return !!this.project && this.project.status === ProjectStatus.PendingApproval;
  }

  get isProjectFrozen(): boolean {
    return !!this.project && this.project.status !== ProjectStatus.Draft;
  }

  openSubmitModal(): void {
    this.submitValidationErrors = [];
    this.validatingSubmit = true;
    this.workflowService.validateSubmit(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.validatingSubmit = false;
          if (res.success && res.data.isValid) {
            this.submitReason = '';
            this.submitError = null;
            this.showSubmitModal = true;
          } else {
            this.submitValidationErrors = res.data?.errors ?? ['Validation failed.'];
          }
        },
        error: () => {
          this.validatingSubmit = false;
          this.submitValidationErrors = ['Failed to validate project. Please try again.'];
        },
      });
  }

  submitForApproval(): void {
    if (!this.submitReason.trim()) return;
    this.submittingApproval = true;
    this.workflowService.submitForApproval({
      projectId: this.projectId,
      reason: this.submitReason.trim(),
    }).subscribe({
      next: () => {
        this.toastSvc.success('Project submitted for approval');
        this.showSubmitModal = false;
        this.submittingApproval = false;
        this.loadProject();
        this.loadApprovalRequests();
      },
      error: (err) => {
        this.submitError = err?.error?.message ?? err?.error?.detail ?? err?.message ?? 'Failed to submit for approval';
        this.submittingApproval = false;
      },
    });
  }

  openReviewModal(isApproval: boolean): void {
    this.reviewIsApproval = isApproval;
    this.reviewComment = '';
    this.pendingRequestId = this.approvalRequests.find(r => r.status === 'Pending')?.id ?? null;
    this.showReviewModal = true;
  }

  reviewApproval(): void {
    if (!this.pendingRequestId) return;
    if (!this.reviewIsApproval && !this.reviewComment.trim()) return;
    this.reviewingApproval = true;
    this.workflowService.reviewApprovalRequest(this.pendingRequestId, {
      requestId: this.pendingRequestId,
      isApproved: this.reviewIsApproval,
      reviewComment: this.reviewComment.trim() || null,
    }).subscribe({
      next: () => {
        const action = this.reviewIsApproval ? 'approved' : 'rejected';
        this.toastSvc.success(`Project ${action} successfully`);
        this.showReviewModal = false;
        this.reviewingApproval = false;
        this.loadProject();
        this.loadApprovalRequests();
        this.loadStats();
      },
      error: (err) => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to review approval');
        this.reviewingApproval = false;
      },
    });
  }

  approvalStatusClass(status: string): string {
    switch (status) {
      case 'Pending':  return 'badge bg-warning text-dark';
      case 'Approved': return 'badge bg-success';
      case 'Rejected': return 'badge bg-danger';
      default:         return 'badge bg-secondary';
    }
  }

  // ── Edit form ──────────────────────────────────────────
  private emptyEditForm(): EditFormData {
    return {
      name: '', description: '',
      projectType: ProjectType.Operational,
      strategicObjectiveId: null,
      priority: ProjectPriority.Medium,
      projectManagerUserId: '',
      sponsorUserIds: [],
      plannedStartDate: '', plannedEndDate: '',
      approvedBudget: null,
      status: ProjectStatus.Draft,
      actualStartDate: '',
    };
  }

  openEditForm(): void {
    const prj = this.project;
    if (!prj) return;

    this.editForm = {
      name: prj.name,
      description: prj.description ?? '',
      projectType: prj.projectType,
      strategicObjectiveId: prj.strategicObjectiveId ?? null,
      priority: prj.priority,
      projectManagerUserId: prj.projectManagerUserId,
      sponsorUserIds: prj.sponsors.map(s => s.userId),
      plannedStartDate: prj.plannedStartDate ? String(prj.plannedStartDate).substring(0, 10) : '',
      plannedEndDate: prj.plannedEndDate ? String(prj.plannedEndDate).substring(0, 10) : '',
      approvedBudget: prj.approvedBudget ?? null,
      status: prj.status,
      actualStartDate: prj.actualStartDate ? String(prj.actualStartDate).substring(0, 10) : '',
    };

    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';

    if (prj.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }

    this.showEditForm = true;
  }

  cancelEditForm(): void {
    this.showEditForm = false;
    this.saveEditError = null;
    this.sponsorDropdownOpen = false;
    this.sponsorSearchTerm = '';
  }

  onProjectTypeChange(): void {
    if (this.editForm.projectType === ProjectType.Strategic) {
      this.loadStrategicObjectives();
    } else {
      this.editForm.strategicObjectiveId = null;
      this.strategicObjectives = [];
      this.strategicObjectivesLoaded = false;
    }
  }

  private loadStrategicObjectives(): void {
    if (!this.project) return;
    this.projectService.getStrategicObjectivesForWorkspace(this.project.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.strategicObjectives = res.data ?? [];
          this.strategicObjectivesLoaded = true;
        },
        error: () => {
          this.strategicObjectives = [];
          this.strategicObjectivesLoaded = true;
        },
      });
  }

  get isProjectStrategic(): boolean {
    return this.editForm.projectType === ProjectType.Strategic;
  }

  get hasDateRangeError(): boolean {
    return !!(this.editForm.plannedStartDate && this.editForm.plannedEndDate
      && this.editForm.plannedEndDate <= this.editForm.plannedStartDate);
  }

  saveEdit(): void {
    if (!this.editForm.name.trim() || !this.editForm.projectManagerUserId
      || this.editForm.sponsorUserIds.length === 0
      || !this.editForm.plannedStartDate || !this.editForm.plannedEndDate
      || this.hasDateRangeError) {
      return;
    }
    if (this.isProjectStrategic && !this.editForm.strategicObjectiveId) {
      return;
    }

    this.saveEditError = null;

    // Validate milestone/action-item structure when transitioning to Active or Completed
    const needsValidation =
      (this.editForm.status === ProjectStatus.Active   && this.project?.status !== ProjectStatus.Active) ||
      (this.editForm.status === ProjectStatus.Completed && this.project?.status !== ProjectStatus.Completed);

    if (needsValidation) {
      this.saving = true;
      this.validateMilestoneStructure(this.editForm.status, () => this.doSaveEdit());
      return;
    }

    this.doSaveEdit();
  }

  private validateMilestoneStructure(targetStatus: ProjectStatus, onValid: () => void): void {
    const label = targetStatus === ProjectStatus.Completed ? 'Completed' : 'Active';
    const actionFilter: ActionItemFilter = {
      projectId:      this.projectId,
      pageNumber:     1,
      pageSize:       500,
      sortBy:         'dueDate',
      sortDescending: false,
    };

    forkJoin({
      milestones: this.milestoneService.getByProject(this.projectId),
      actions:    this.actionService.getAll(actionFilter),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ milestones, actions }) => {
        const msList     = milestones.data ?? [];
        const actionList = (actions.data as PagedResult<ActionItem>).items ?? [];

        if (msList.length === 0) {
          this.saveEditError = `Cannot set to ${label}: the project must have at least one milestone.`;
          this.saving = false;
          return;
        }

        const emptyMilestones = msList.filter(ms =>
          !actionList.some(a => a.milestoneId === ms.id)
        );

        if (emptyMilestones.length > 0) {
          const names = emptyMilestones.map(m => `"${m.name}"`).join(', ');
          this.saveEditError =
            `Cannot set to ${label}: the following milestone(s) have no action items — ${names}. ` +
            `Please add at least one action item to each milestone first.`;
          this.saving = false;
          return;
        }

        // When completing, all action items must be Done or Cancelled
        if (targetStatus === ProjectStatus.Completed) {
          const doneStatuses: (string | number)[] = [
            ActionStatus.Done, ActionStatus.Cancelled,
            'done', 'cancelled',
          ];
          const incompleteActions = actionList.filter(a =>
            !doneStatuses.includes(a.status as string | number)
          );

          if (incompleteActions.length > 0) {
            const names = incompleteActions.map(a => `"${a.title}"`).join(', ');
            this.saveEditError =
              `Cannot complete the project: all action items must be Done or Cancelled. ` +
              `Incomplete action items: ${names}.`;
            this.saving = false;
            return;
          }
        }

        onValid();
      },
      error: () => {
        this.saveEditError = 'Could not validate milestones. Please try again.';
        this.saving = false;
      },
    });
  }

  private doSaveEdit(): void {
    this.saving = true;

    const payload: ProjectUpdate = {
      name:                   this.editForm.name.trim(),
      description:            this.editForm.description?.trim() || undefined,
      projectType:            this.editForm.projectType,
      status:                 this.editForm.status,
      strategicObjectiveId:   this.editForm.strategicObjectiveId || undefined,
      priority:               this.editForm.priority,
      projectManagerUserId:   this.editForm.projectManagerUserId,
      sponsorUserIds:         this.editForm.sponsorUserIds,
      plannedStartDate:       this.editForm.plannedStartDate,
      plannedEndDate:         this.editForm.plannedEndDate,
      actualStartDate:        this.editForm.actualStartDate || undefined,
      approvedBudget:         this.editForm.approvedBudget ? +this.editForm.approvedBudget : undefined,
    };

    this.projectService.update(this.projectId, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving = false;
          this.showEditForm = false;
          this.saveEditError = null;
          this.toastSvc.success('Project updated.');
          this.loadProject();
          this.loadStats();
        },
        error: (err) => {
          this.saving = false;
          this.saveEditError = err?.error?.message ?? err?.error?.detail ?? 'Failed to update project.';
        },
      });
  }

  // ── Sponsor helpers ────────────────────────────────────
  @HostListener('document:click')
  onDocumentClick(): void {
    this.sponsorDropdownOpen = false;
  }

  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.allUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.allUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  toggleSponsor(userId: string): void {
    const idx = this.editForm.sponsorUserIds.indexOf(userId);
    if (idx >= 0) this.editForm.sponsorUserIds.splice(idx, 1);
    else this.editForm.sponsorUserIds.push(userId);
  }

  isSponsorSelected(userId: string): boolean {
    return this.editForm.sponsorUserIds.includes(userId);
  }

  getSponsorName(userId: string): string {
    return this.allUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  // ── Gantt ───────────────────────────────────────────────
  toggleGantt(): void {
    this.showGantt = !this.showGantt;
    if (this.showGantt && !this.ganttLoaded) {
      this.loadGanttData();
    }
  }

  private loadGanttData(): void {
    this.ganttLoading = true;
    const actionFilter: ActionItemFilter = {
      projectId: this.projectId,
      pageNumber: 1,
      pageSize: 500,
      sortBy: 'dueDate',
      sortDescending: false,
    };
    forkJoin({
      milestones: this.milestoneService.getByProject(this.projectId),
      actions:    this.actionService.getAll(actionFilter),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ milestones, actions }) => {
        this.ganttMilestones = (milestones.data ?? [])
          .sort((a, b) => a.sequenceOrder - b.sequenceOrder);
        this.ganttActions = (actions.data as PagedResult<ActionItem>).items ?? [];
        this.ganttLoaded  = true;
        this.ganttLoading = false;
      },
      error: () => { this.ganttLoading = false; },
    });
  }

  get ganttRange(): { start: Date; end: Date; totalMs: number } {
    const prj = this.project!;
    let start = new Date(prj.plannedStartDate);
    let end   = new Date(prj.plannedEndDate);

    for (const ms of this.ganttMilestones) {
      if (ms.plannedStartDate) { const d = new Date(ms.plannedStartDate); if (d < start) start = d; }
      if (ms.plannedDueDate)   { const d = new Date(ms.plannedDueDate);   if (d > end)   end   = d; }
    }
    for (const a of this.ganttActions) {
      if (a.startDate) { const d = new Date(a.startDate); if (d < start) start = d; }
      if (a.dueDate)   { const d = new Date(a.dueDate);   if (d > end)   end   = d; }
    }
    // 5-day padding on each side
    start = new Date(start.getTime() - 5 * 86_400_000);
    end   = new Date(end.getTime()   + 5 * 86_400_000);
    return { start, end, totalMs: end.getTime() - start.getTime() };
  }

  ganttBarStyle(startStr: string | null, endStr: string | null): Record<string, string> {
    if (!endStr) return { display: 'none' };
    const { start, totalMs } = this.ganttRange;
    const s = startStr ? new Date(startStr) : new Date(endStr);
    const e = new Date(endStr);
    const leftMs  = Math.max(0, s.getTime() - start.getTime());
    const widthMs = Math.max(86_400_000, e.getTime() - s.getTime()); // min 1 day
    return {
      left:  `${(leftMs  / totalMs * 100).toFixed(2)}%`,
      width: `${(widthMs / totalMs * 100).toFixed(2)}%`,
    };
  }

  get ganttMonths(): { label: string; left: number; width: number }[] {
    const { start, end, totalMs } = this.ganttRange;
    const months: { label: string; left: number; width: number }[] = [];
    const cur = new Date(start.getFullYear(), start.getMonth(), 1);
    while (cur <= end) {
      const mStart = new Date(cur);
      const mEnd   = new Date(cur.getFullYear(), cur.getMonth() + 1, 1);
      const left   = Math.max(0,   (mStart.getTime() - start.getTime()) / totalMs * 100);
      const right  = Math.min(100, (mEnd.getTime()   - start.getTime()) / totalMs * 100);
      if (right > left) {
        months.push({
          label: mStart.toLocaleDateString('en-US', { month: 'short', year: '2-digit' }),
          left,
          width: right - left,
        });
      }
      cur.setMonth(cur.getMonth() + 1);
    }
    return months;
  }

  get todayLeft(): number {
    const { start, totalMs } = this.ganttRange;
    return Math.max(0, Math.min(100,
      (Date.now() - start.getTime()) / totalMs * 100
    ));
  }

  ganttActionsForMilestone(milestoneId: string): ActionItem[] {
    return this.ganttActions.filter(a => a.milestoneId === milestoneId);
  }

  get ganttStandaloneActions(): ActionItem[] {
    return this.ganttActions.filter(a => !a.milestoneId);
  }

  ganttActionBarClass(item: ActionItem): string {
    const STATUS_MAP: Record<string, ActionStatus> = {
      todo: ActionStatus.ToDo, inprogress: ActionStatus.InProgress,
      inreview: ActionStatus.InReview, done: ActionStatus.Done,
      overdue: ActionStatus.Overdue,
    };
    const st = typeof item.status === 'number'
      ? item.status
      : STATUS_MAP[String(item.status).toLowerCase()] ?? ActionStatus.ToDo;
    switch (st) {
      case ActionStatus.Done:       return 'gantt-bar--done';
      case ActionStatus.Overdue:    return 'gantt-bar--overdue';
      case ActionStatus.InProgress: return 'gantt-bar--inprogress';
      case ActionStatus.InReview:   return 'gantt-bar--inreview';
      default:                      return 'gantt-bar--todo';
    }
  }

  showGanttTooltip(
    event: MouseEvent,
    type: 'project' | 'milestone' | 'action',
    title: string,
    status: string,
    startDate: string | null,
    endDate: string | null,
    extra?: { progress?: number; priority?: string; assignees?: string[] },
  ): void {
    this.ganttTooltip = { type, title, status, startDate, endDate, ...extra, ...this.tooltipPos(event) };
  }

  moveGanttTooltip(event: MouseEvent): void {
    if (this.ganttTooltip) {
      Object.assign(this.ganttTooltip, this.tooltipPos(event));
    }
  }

  hideGanttTooltip(): void {
    this.ganttTooltip = null;
  }

  actionAssigneeNames(action: ActionItem): string[] {
    return action.assignees.map(a => a.fullName);
  }

  private tooltipPos(event: MouseEvent): { x: number; y: number } {
    const offset = 14;
    const tw = 280; // approx tooltip width
    const th = 200; // approx tooltip height
    const x = (event.clientX + offset + tw > window.innerWidth)
      ? event.clientX - tw - offset
      : event.clientX + offset;
    const y = (event.clientY + offset + th > window.innerHeight)
      ? event.clientY - th - offset
      : event.clientY + offset;
    return { x, y };
  }

  // ── Export ─────────────────────────────────────────────
  exportToExcel(): void {
    const prj = this.project;
    if (!prj) return;

    const wb = XLSX.utils.book_new();

    // Sheet 1: Project Info
    const infoRows: Record<string, unknown>[] = [
      { Field: 'Code',                    Value: prj.projectCode },
      { Field: 'Name',                    Value: prj.name },
      { Field: 'Workspace',               Value: prj.workspaceTitle },
      { Field: 'Type',                    Value: prj.projectTypeLabel },
      { Field: 'Status',                  Value: prj.statusLabel },
      { Field: 'Priority',                Value: prj.priorityLabel },
      { Field: 'Manager',                 Value: prj.projectManagerName },
      { Field: 'Sponsors',                Value: prj.sponsors.map(s => s.fullName).join(', ') },
      { Field: 'Org Unit',                Value: prj.ownerOrgUnitName ?? '' },
      { Field: 'Planned Start',           Value: prj.plannedStartDate  ? new Date(prj.plannedStartDate).toLocaleDateString()  : '' },
      { Field: 'Planned End',             Value: prj.plannedEndDate    ? new Date(prj.plannedEndDate).toLocaleDateString()    : '' },
      { Field: 'Actual Start',            Value: prj.actualStartDate   ? new Date(prj.actualStartDate).toLocaleDateString()   : '' },
      { Field: 'Approved Budget',         Value: prj.approvedBudget != null ? `${prj.approvedBudget} ${prj.currency}` : '' },
      { Field: 'Baselined',               Value: prj.isBaselined ? 'Yes' : 'No' },
      { Field: 'Strategic Objective',     Value: prj.strategicObjectiveStatement ?? '' },
      { Field: 'Description',             Value: prj.description ?? '' },
      { Field: 'Created',                 Value: prj.createdAt ? new Date(prj.createdAt).toLocaleDateString() : '' },
      { Field: 'Last Updated',            Value: prj.updatedAt ? new Date(prj.updatedAt).toLocaleDateString() : '' },
    ];
    if (this.stats) {
      infoRows.push(
        { Field: 'Milestones',      Value: this.stats.milestoneCount },
        { Field: 'Action Items',    Value: this.stats.actionItemCount },
        { Field: 'Completion Rate', Value: `${this.stats.completionRate}%` },
        { Field: 'On-Time Rate',    Value: `${this.stats.onTimeRate}%` },
        { Field: 'Escalated',       Value: this.stats.escalatedCount },
      );
    }
    XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(infoRows), 'Project Info');

    const filename = `project-${prj.projectCode}-${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
  }

  printToPDF(): void {
    window.print();
  }

  // ── Display helpers ────────────────────────────────────
  priorityClass(p: ProjectPriority): string {
    switch (p) {
      case ProjectPriority.Critical: return 'badge bg-danger';
      case ProjectPriority.High:     return 'badge bg-warning text-dark';
      case ProjectPriority.Medium:   return 'badge bg-info text-dark';
      case ProjectPriority.Low:      return 'badge bg-secondary';
      default:                       return 'badge bg-light text-dark';
    }
  }

  statusClass(s: ProjectStatus): string {
    switch (s) {
      case ProjectStatus.Draft:     return 'badge bg-secondary';
      case ProjectStatus.Active:    return 'badge bg-primary';
      case ProjectStatus.OnHold:    return 'badge bg-warning text-dark';
      case ProjectStatus.Completed: return 'badge bg-success';
      case ProjectStatus.Cancelled:       return 'badge bg-danger';
      case ProjectStatus.PendingApproval: return 'badge bg-warning text-dark';
      default:                            return 'badge bg-light text-dark';
    }
  }

  typeClass(t: ProjectType): string {
    switch (t) {
      case ProjectType.Strategic:   return 'badge bg-primary';
      case ProjectType.Operational: return 'badge bg-info text-dark';
      default:                      return 'badge bg-light text-dark';
    }
  }
}
