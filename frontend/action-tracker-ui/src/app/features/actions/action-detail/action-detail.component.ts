import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, computed, HostListener,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ActionItemService }     from '../../../core/services/action-item.service';
import { ToastService }          from '../../../core/services/toast.service';

import {
  ActionItem, ActionItemCreate, ActionStatus, ActionPriority,
  AssignableUser, EscalationInfo,
} from '../../../core/models/action-item.model';

import { CommentsSectionComponent }  from '../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../shared/components/documents-section/documents-section.component';
import { BreadcrumbComponent }       from '../../../shared/components/breadcrumb/breadcrumb.component';

interface EditFormData {
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
  selector: 'app-action-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule, RouterLink, FormsModule,
    CommentsSectionComponent, DocumentsSectionComponent,
    BreadcrumbComponent,
  ],
  templateUrl: './action-detail.component.html',
  styleUrl:    './action-detail.component.scss',
})
export class ActionDetailComponent implements OnInit {
  private readonly route      = inject(ActivatedRoute);
  private readonly actionSvc  = inject(ActionItemService);
  private readonly toastSvc   = inject(ToastService);

  readonly item       = signal<ActionItem | null>(null);
  readonly loading    = signal(true);

  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;

  readonly workspaceId = computed(() => this.item()?.workspaceId ?? '');

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
    this.loadUsers();
  }

  // ── Data loading ───────────────────────────────────────
  private loadItem(id: string): void {
    this.loading.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        this.item.set(r.data);
        this.loading.set(false);
      },
      error: () => {
        this.toastSvc.error('Failed to load action item.');
        this.loading.set(false);
      },
    });
  }

  private loadUsers(): void {
    this.actionSvc.getAssignableUsers().subscribe({
      next: r => { this.allUsers = r.data ?? []; },
      error: () => {},
    });
  }

  // ── Edit form ──────────────────────────────────────────
  private emptyEditForm(): EditFormData {
    return {
      title: '', description: '', assigneeIds: [],
      priority: ActionPriority.Medium, status: ActionStatus.ToDo,
      startDate: '', dueDate: '', progress: 0,
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

  saveEdit(): void {
    const ai = this.item();
    if (!ai) return;
    if (!this.editForm.title.trim() || this.editForm.assigneeIds.length === 0 || !this.editForm.dueDate) return;
    if (this.editForm.isEscalated && !this.editForm.escalationExplanation?.trim()) return;

    this.saving = true;

    const escalatedChanged = this.editForm.isEscalated !== this.originalEscalated;
    const explanationChanged = this.editForm.escalationExplanation?.trim() !== this.originalEscalationText.trim();
    const shouldSendEscalation = escalatedChanged || explanationChanged;

    const payload: Partial<ActionItemCreate> = {
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

  // ── Assignee helpers ───────────────────────────────────
  @HostListener('document:click')
  onDocumentClick(): void {
    this.assigneeDropdownOpen = false;
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
