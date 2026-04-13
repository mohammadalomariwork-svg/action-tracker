import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy,
  inject, signal, computed,
} from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { ToastService }      from '../../../core/services/toast.service';
import { WorkspaceService }  from '../../workspaces/services/workspace.service';
import { WorkflowService }   from '../../../services/workflow.service';

import {
  ActionItem, ActionItemCreate,
  ActionStatus, ActionPriority, AssignableUser,
} from '../../../core/models/action-item.model';
import {
  WorkflowRequest,
  CreateDateChangeRequest,
  CreateStatusChangeRequest,
  WORKFLOW_STATUS_CONFIG,
  WORKFLOW_TYPE_LABELS,
} from '../../../models/workflow.model';
import { WorkspaceList }     from '../../workspaces/models/workspace.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';

// ── Option lists ──────────────────────────────────────────────────────────────

export const PRIORITY_OPTIONS: { value: ActionPriority; label: string; color: string; dot: string }[] = [
  { value: ActionPriority.Critical, label: 'Critical', color: '#dc2626', dot: '🔴' },
  { value: ActionPriority.High,     label: 'High',     color: '#ea580c', dot: '🟠' },
  { value: ActionPriority.Medium,   label: 'Medium',   color: '#d97706', dot: '🟡' },
  { value: ActionPriority.Low,      label: 'Low',      color: '#059669', dot: '🟢' },
];

export const STATUS_OPTIONS: { value: ActionStatus; label: string }[] = [
  { value: ActionStatus.ToDo,       label: 'To Do'       },
  { value: ActionStatus.InProgress, label: 'In Progress' },
  { value: ActionStatus.InReview,   label: 'In Review'   },
  { value: ActionStatus.Done,       label: 'Done'        },
  { value: ActionStatus.Overdue,    label: 'Overdue'     },
];

const DESC_MAX = 5000;
const TODAY = new Date().toISOString().slice(0, 10);

@Component({
  selector: 'app-action-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, CommonModule, FormsModule, RouterLink, PageHeaderComponent, BreadcrumbComponent],
  templateUrl: './action-form.component.html',
  styleUrl:    './action-form.component.scss',
})
export class ActionFormComponent implements OnInit, OnDestroy {
  private readonly fb            = inject(FormBuilder);
  private readonly route         = inject(ActivatedRoute);
  private readonly router        = inject(Router);
  private readonly actionSvc     = inject(ActionItemService);
  private readonly toastSvc      = inject(ToastService);
  private readonly workspaceSvc  = inject(WorkspaceService);
  private readonly workflowSvc   = inject(WorkflowService);
  private readonly destroy$      = new Subject<void>();

  // ── State ─────────────────────────────────────────────
  readonly isEditMode   = signal(false);
  readonly editItem     = signal<ActionItem | null>(null);
  readonly teamMembers  = signal<AssignableUser[]>([]);
  readonly workspaces   = signal<WorkspaceList[]>([]);
  readonly saving       = signal(false);
  readonly loadingItem  = signal(false);

  // ── Workflow state ────────────────────────────────────
  readonly areDatesLocked      = signal(false);
  readonly pendingRequests     = signal<WorkflowRequest[]>([]);
  readonly canReview           = signal(false);
  readonly showDateChangeDialog   = signal(false);
  readonly showStatusChangeDialog = signal(false);
  readonly dateChangeReason    = signal('');
  readonly statusChangeReason  = signal('');
  readonly selectedNewStatus   = signal<number | null>(null);
  readonly requestedNewStartDate = signal('');
  readonly requestedNewDueDate   = signal('');
  readonly showWorkflowHistory = signal(false);

  readonly WORKFLOW_STATUS_CONFIG = WORKFLOW_STATUS_CONFIG;
  readonly WORKFLOW_TYPE_LABELS   = WORKFLOW_TYPE_LABELS;

  /** Terminal statuses that standalone items cannot directly transition to */
  readonly RESTRICTED_STATUSES = [ActionStatus.Done, ActionStatus.Cancelled, ActionStatus.Deferred];

  readonly filteredStatusOptions = computed(() => {
    const item = this.editItem();
    if (item?.isStandalone && this.isEditMode()) {
      return STATUS_OPTIONS.filter(o => !this.RESTRICTED_STATUSES.includes(o.value));
    }
    return STATUS_OPTIONS;
  });

  readonly pageTitle = computed(() => {
    const item = this.editItem();
    return item
      ? `Edit: ${item.actionId} – ${item.title}`
      : 'New Action Item';
  });

  readonly pageIcon = computed(() => this.isEditMode() ? 'bi bi-pencil-square' : 'bi bi-plus-circle');

  readonly descLength  = computed(() => (this.form?.get('description')?.value ?? '').length);
  readonly progressVal = computed(() => +(this.form?.get('progress')?.value ?? 0));

  // ── Constants exposed to template ─────────────────────
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly ActionStatus     = ActionStatus;
  readonly DESC_MAX  = DESC_MAX;
  readonly TODAY     = TODAY;

  // ── Form ──────────────────────────────────────────────
  readonly form: FormGroup = this.fb.group({
    title:       ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(DESC_MAX)]],
    workspaceId: ['', Validators.required],
    assigneeIds: [[] as string[], Validators.required],
    priority:    ['', Validators.required],
    status:      [ActionStatus.ToDo, Validators.required],
    startDate:   [''],
    dueDate:     ['', Validators.required],
    progress:    [0,  [Validators.min(0), Validators.max(100)]],
  });

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    this.actionSvc.getAssignableUsers().subscribe({
      next: r => this.teamMembers.set(r.data ?? []),
      error: () => {},
    });

    this.workspaceSvc.getWorkspaces().subscribe({
      next: r => this.workspaces.set((r.data ?? []).filter(w => w.isActive)),
      error: () => {},
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode.set(true);
      this.loadForEdit(id);
    }

    this.wireValueChanges();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Load for edit ─────────────────────────────────────
  private loadForEdit(id: string): void {
    this.loadingItem.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        const item = r.data;

        // Block editing completed items
        if (item.statusCode === ActionStatus.Done) {
          this.toastSvc.error('Completed action items cannot be edited.');
          this.router.navigate(['/actions']);
          return;
        }

        this.editItem.set(item);
        this.form.patchValue({
          title:       item.title,
          description: item.description,
          workspaceId: item.workspaceId,
          assigneeIds: item.assignees.map(a => a.userId),
          priority:    item.priority,
          status:      item.status,
          startDate:   item.startDate ? item.startDate.slice(0, 10) : '',
          dueDate:     item.dueDate.slice(0, 10),
          progress:    item.progress,
          // isEscalated is managed from the detail view, not the form
        });
        this.loadingItem.set(false);

        // Workflow: lock dates for standalone items
        if (item.isStandalone) {
          this.areDatesLocked.set(true);
          this.loadWorkflowData(item.id);
        }
      },
      error: () => {
        this.loadingItem.set(false);
        this.toastSvc.error('Failed to load action item.');
        this.router.navigate(['/actions']);
      },
    });
  }

  // ── Workflow data loading ─────────────────────────────
  private loadWorkflowData(actionItemId: string): void {
    this.workflowSvc.getRequestsForActionItem(actionItemId).subscribe({
      next: r => this.pendingRequests.set(
        (r.data ?? []).filter(req => req.status === 'Pending')
      ),
      error: () => {},
    });
    this.workflowSvc.canReview(actionItemId).subscribe({
      next: r => this.canReview.set(r.data?.canReview ?? false),
      error: () => {},
    });
  }

  // ── Date change request ───────────────────────────────
  openDateChangeDialog(): void {
    const item = this.editItem();
    this.requestedNewStartDate.set(item?.startDate ? item.startDate.slice(0, 10) : '');
    this.requestedNewDueDate.set(item?.dueDate ? item.dueDate.slice(0, 10) : '');
    this.dateChangeReason.set('');
    this.showDateChangeDialog.set(true);
  }

  submitDateChangeRequest(): void {
    const item = this.editItem();
    if (!item) return;

    const dto: CreateDateChangeRequest = {
      actionItemId: item.id,
      newStartDate: this.requestedNewStartDate() || null,
      newDueDate: this.requestedNewDueDate() || null,
      reason: this.dateChangeReason().trim(),
    };

    this.workflowSvc.createDateChangeRequest(dto).subscribe({
      next: () => {
        this.toastSvc.success('Date change request submitted for approval.');
        this.showDateChangeDialog.set(false);
        this.loadWorkflowData(item.id);
      },
      error: err => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit date change request.');
      },
    });
  }

  // ── Status change request ─────────────────────────────
  openStatusChangeDialog(): void {
    this.selectedNewStatus.set(null);
    this.statusChangeReason.set('');
    this.showStatusChangeDialog.set(true);
  }

  submitStatusChangeRequest(): void {
    const item = this.editItem();
    const newStatus = this.selectedNewStatus();
    if (!item || newStatus === null) return;

    const dto: CreateStatusChangeRequest = {
      actionItemId: item.id,
      newStatus: newStatus,
      reason: this.statusChangeReason().trim(),
    };

    this.workflowSvc.createStatusChangeRequest(dto).subscribe({
      next: () => {
        this.toastSvc.success('Status change request submitted for approval.');
        this.showStatusChangeDialog.set(false);
        this.loadWorkflowData(item.id);
      },
      error: err => {
        this.toastSvc.error(err?.error?.message ?? 'Failed to submit status change request.');
      },
    });
  }

  // ── Cross-field value change logic ────────────────────
  private wireValueChanges(): void {
    // Status → Done auto-sets progress to 100
    this.form.get('status')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status: ActionStatus) => {
        if (+status === ActionStatus.Done) {
          this.form.get('progress')!.setValue(100, { emitEvent: false });
        }
      });

    // Progress → 100 offers to set Status=Done
    this.form.get('progress')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((val: number) => {
        if (+val === 100 && +this.form.get('status')!.value !== ActionStatus.Done) {
          if (confirm('Progress is 100%. Mark this action item as Done?')) {
            this.form.get('status')!.setValue(ActionStatus.Done, { emitEvent: false });
          }
        }
      });
  }

  // ── Assignee multi-select helpers ─────────────────────
  get selectedAssigneeIds(): string[] {
    return this.form.get('assigneeIds')!.value ?? [];
  }

  toggleAssignee(userId: string): void {
    const current = [...this.selectedAssigneeIds];
    const idx = current.indexOf(userId);
    if (idx >= 0) {
      current.splice(idx, 1);
    } else {
      current.push(userId);
    }
    this.form.get('assigneeIds')!.setValue(current);
    this.form.get('assigneeIds')!.markAsTouched();
  }

  isAssigneeSelected(userId: string): boolean {
    return this.selectedAssigneeIds.includes(userId);
  }

  // ── Accessors ─────────────────────────────────────────
  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  isInvalid(name: string): boolean {
    const c = this.ctrl(name);
    return c.invalid && c.touched;
  }

  // ── Submit ────────────────────────────────────────────
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    const payload: ActionItemCreate = {
      title:       raw.title.trim(),
      description: raw.description?.trim() ?? '',
      workspaceId: raw.workspaceId,
      isStandalone: true,
      assigneeIds: raw.assigneeIds,
      priority:    +raw.priority,
      status:      +raw.status,
      startDate:   raw.startDate || null,
      dueDate:     raw.dueDate,
      progress:    +raw.progress,
    };

    const item = this.editItem();
    const obs$ = item
      ? this.actionSvc.update(item.id, payload)
      : this.actionSvc.create(payload);

    obs$.subscribe({
      next: () => {
        this.saving.set(false);
        this.toastSvc.success(item ? 'Action item updated.' : 'Action item created.');
        this.router.navigate(['/actions']);
      },
      error: (err) => {
        this.saving.set(false);
        if (err?.status === 422) {
          const msg = (err?.error?.message ?? '').toLowerCase();
          if (msg.includes('date')) {
            this.openDateChangeDialog();
            this.toastSvc.warning('Direct date changes are not allowed for standalone items. Please submit a request.');
          } else if (msg.includes('status')) {
            this.openStatusChangeDialog();
            this.toastSvc.warning('Direct status changes are not allowed for standalone items. Please submit a request.');
          } else {
            this.toastSvc.error(err?.error?.message ?? 'Failed to save action item. Please try again.');
          }
        } else {
          this.toastSvc.error('Failed to save action item. Please try again.');
        }
      },
    });
  }
}
