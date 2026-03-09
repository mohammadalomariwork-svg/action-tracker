import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MilestoneService } from '../../services/milestone.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ProjectService } from '../../services/project.service';
import {
  MilestoneResponse,
  MilestoneCreate,
  MilestoneUpdate,
  MilestoneStatus,
  MilestoneStatusLabels,
} from '../../models/milestone.models';
import { AssignableUser } from '../../../../core/models/action-item.model';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';

@Component({
  selector: 'app-milestone-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, CommentsSectionComponent, DocumentsSectionComponent],
  templateUrl: './milestone-section.component.html',
  styleUrl: './milestone-section.component.scss',
})
export class MilestoneSectionComponent implements OnInit {
  readonly projectId = input.required<string>();
  readonly isBaselined = input<boolean>(false);

  private readonly milestoneSvc = inject(MilestoneService);
  private readonly projectSvc = inject(ProjectService);
  private readonly toastSvc = inject(ToastService);

  readonly milestones = signal<MilestoneResponse[]>([]);
  readonly loading = signal(false);
  readonly users = signal<AssignableUser[]>([]);

  // Form state
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly submitting = signal(false);

  // Expanded milestone (for comments/documents)
  readonly expandedId = signal<string | null>(null);

  // Form model
  readonly formName = signal('');
  readonly formDescription = signal('');
  readonly formSequenceOrder = signal(1);
  readonly formPlannedStartDate = signal('');
  readonly formPlannedDueDate = signal('');
  readonly formActualCompletionDate = signal('');
  readonly formIsDeadlineFixed = signal(false);
  readonly formStatus = signal<MilestoneStatus>(MilestoneStatus.NotStarted);
  readonly formCompletionPercentage = signal(0);
  readonly formWeight = signal(0);
  readonly formApproverUserId = signal('');

  readonly MilestoneStatus = MilestoneStatus;
  readonly MilestoneStatusLabels = MilestoneStatusLabels;
  readonly statusOptions = [
    MilestoneStatus.NotStarted,
    MilestoneStatus.InProgress,
    MilestoneStatus.Completed,
    MilestoneStatus.Delayed,
    MilestoneStatus.Cancelled,
  ];

  ngOnInit(): void {
    this.loadMilestones();
    this.loadUsers();
  }

  private loadMilestones(): void {
    this.loading.set(true);
    this.milestoneSvc.getByProject(this.projectId()).subscribe({
      next: r => {
        this.milestones.set(r.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toastSvc.error('Failed to load milestones.');
      },
    });
  }

  private loadUsers(): void {
    this.projectSvc.getAssignableUsers().subscribe({
      next: r => this.users.set(r.data ?? []),
      error: () => {},
    });
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.resetForm();
    const nextSeq = this.milestones().length > 0
      ? Math.max(...this.milestones().map(m => m.sequenceOrder)) + 1
      : 1;
    this.formSequenceOrder.set(nextSeq);
    this.showForm.set(true);
  }

  openEditForm(m: MilestoneResponse): void {
    this.editingId.set(m.id);
    this.formName.set(m.name);
    this.formDescription.set(m.description ?? '');
    this.formSequenceOrder.set(m.sequenceOrder);
    this.formPlannedStartDate.set(m.plannedStartDate?.substring(0, 10) ?? '');
    this.formPlannedDueDate.set(m.plannedDueDate?.substring(0, 10) ?? '');
    this.formActualCompletionDate.set(m.actualCompletionDate?.substring(0, 10) ?? '');
    this.formIsDeadlineFixed.set(m.isDeadlineFixed);
    this.formStatus.set(m.status);
    this.formCompletionPercentage.set(m.completionPercentage);
    this.formWeight.set(m.weight);
    this.formApproverUserId.set(m.approverUserId ?? '');
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.resetForm();
  }

  submitForm(): void {
    if (!this.formName().trim()) {
      this.toastSvc.warning('Name is required.');
      return;
    }
    this.submitting.set(true);
    const editing = this.editingId();
    if (editing) {
      this.doUpdate(editing);
    } else {
      this.doCreate();
    }
  }

  private doCreate(): void {
    const dto: MilestoneCreate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      isDeadlineFixed: this.formIsDeadlineFixed(),
      completionPercentage: this.formCompletionPercentage(),
      weight: this.formWeight(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.create(this.projectId(), dto).subscribe({
      next: r => {
        this.milestones.update(list => [...list, r.data].sort((a, b) => a.sequenceOrder - b.sequenceOrder));
        this.showForm.set(false);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone created.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to create milestone.');
      },
    });
  }

  private doUpdate(milestoneId: string): void {
    const dto: MilestoneUpdate = {
      name: this.formName().trim(),
      description: this.formDescription().trim() || undefined,
      sequenceOrder: this.formSequenceOrder(),
      plannedStartDate: this.formPlannedStartDate(),
      plannedDueDate: this.formPlannedDueDate(),
      actualCompletionDate: this.formActualCompletionDate() || undefined,
      isDeadlineFixed: this.formIsDeadlineFixed(),
      status: this.formStatus(),
      completionPercentage: this.formCompletionPercentage(),
      weight: this.formWeight(),
      approverUserId: this.formApproverUserId() || undefined,
    };

    this.milestoneSvc.update(this.projectId(), milestoneId, dto).subscribe({
      next: r => {
        this.milestones.update(list =>
          list.map(m => m.id === milestoneId ? r.data : m)
            .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
        );
        this.showForm.set(false);
        this.editingId.set(null);
        this.resetForm();
        this.submitting.set(false);
        this.toastSvc.success('Milestone updated.');
      },
      error: (err) => {
        this.submitting.set(false);
        this.toastSvc.error(err?.error?.message ?? 'Failed to update milestone.');
      },
    });
  }

  deleteMilestone(m: MilestoneResponse): void {
    if (!confirm(`Delete milestone "${m.name}"?`)) return;

    this.milestoneSvc.delete(this.projectId(), m.id).subscribe({
      next: () => {
        this.milestones.update(list => list.filter(x => x.id !== m.id));
        this.toastSvc.success('Milestone deleted.');
      },
      error: () => this.toastSvc.error('Failed to delete milestone.'),
    });
  }

  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  milestoneStatusClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'badge bg-secondary';
      case MilestoneStatus.InProgress: return 'badge bg-primary';
      case MilestoneStatus.Completed:  return 'badge bg-success';
      case MilestoneStatus.Delayed:    return 'badge bg-warning text-dark';
      case MilestoneStatus.Cancelled:  return 'badge bg-danger';
      default:                         return 'badge bg-light text-dark';
    }
  }

  varianceClass(days: number | null | undefined): string {
    if (days == null) return '';
    if (days > 0) return 'text-danger';
    if (days < 0) return 'text-success';
    return '';
  }

  varianceLabel(days: number | null | undefined): string {
    if (days == null) return 'N/A';
    if (days === 0) return 'On track';
    if (days > 0) return `${days}d behind`;
    return `${Math.abs(days)}d ahead`;
  }

  private resetForm(): void {
    this.formName.set('');
    this.formDescription.set('');
    this.formSequenceOrder.set(1);
    this.formPlannedStartDate.set('');
    this.formPlannedDueDate.set('');
    this.formActualCompletionDate.set('');
    this.formIsDeadlineFixed.set(false);
    this.formStatus.set(MilestoneStatus.NotStarted);
    this.formCompletionPercentage.set(0);
    this.formWeight.set(0);
    this.formApproverUserId.set('');
  }
}
