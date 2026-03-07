import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { MilestoneService } from '../../../services/milestone.service';
import { MilestoneFormComponent } from '../milestone-form/milestone-form.component';
import {
  MilestoneDetail,
  MilestoneStatus,
  ActionItemStatus,
  ActionItemPriority,
} from '../../../models/project.models';

@Component({
  selector: 'app-milestone-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MilestoneFormComponent],
  templateUrl: './milestone-list.component.html',
  styleUrl: './milestone-list.component.scss',
})
export class MilestoneListComponent implements OnInit {
  @Input({ required: true }) projectId!: number;
  @Input() isBaselined = false;
  @Input() canEdit = false;

  private readonly milestoneService = inject(MilestoneService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  milestones: MilestoneDetail[] = [];
  isLoading = false;
  errorMessage: string | null = null;

  showForm = false;
  editingMilestone: MilestoneDetail | undefined = undefined;
  expandedMilestoneId: number | null = null;

  newMilestoneForm!: FormGroup;

  // Expose enums to template
  readonly MilestoneStatus = MilestoneStatus;
  readonly ActionItemStatus = ActionItemStatus;
  readonly ActionItemPriority = ActionItemPriority;

  ngOnInit(): void {
    this.buildNewMilestoneForm();
    this.loadMilestones();
  }

  // ── Accordion toggle ───────────────────────────────────────────────────────

  toggleAccordion(milestoneId: number): void {
    this.expandedMilestoneId =
      this.expandedMilestoneId === milestoneId ? null : milestoneId;
  }

  isExpanded(milestoneId: number): boolean {
    return this.expandedMilestoneId === milestoneId;
  }

  // ── CRUD actions ───────────────────────────────────────────────────────────

  onAddMilestone(): void {
    this.editingMilestone = undefined;
    this.showForm = !this.showForm;
    if (this.showForm) {
      this.newMilestoneForm.reset({
        title: '',
        description: '',
        sequenceOrder: this.milestones.length + 1,
        plannedStartDate: '',
        plannedEndDate: '',
      });
    }
  }

  onEditMilestone(milestone: MilestoneDetail): void {
    this.editingMilestone = milestone;
    this.showForm = false;
  }

  onCancelEdit(): void {
    this.editingMilestone = undefined;
  }

  onMilestoneSaved(): void {
    this.editingMilestone = undefined;
    this.showForm = false;
    this.loadMilestones();
  }

  onDeleteMilestone(id: number): void {
    if (!confirm('Are you sure you want to delete this milestone?')) return;

    this.milestoneService
      .delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadMilestones(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete milestone.';
        },
      });
  }

  onSubmitNewMilestone(): void {
    if (this.newMilestoneForm.invalid) {
      this.newMilestoneForm.markAllAsTouched();
      return;
    }

    const v = this.newMilestoneForm.value;
    this.milestoneService
      .create({
        projectId: this.projectId,
        title: v.title,
        description: v.description || undefined,
        sequenceOrder: v.sequenceOrder,
        plannedStartDate: v.plannedStartDate,
        plannedEndDate: v.plannedEndDate,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.showForm = false;
          this.loadMilestones();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to create milestone.';
        },
      });
  }

  onUpdateProgress(milestone: MilestoneDetail, percentage: number): void {
    this.milestoneService
      .update(milestone.id, {
        id: milestone.id,
        title: milestone.title,
        description: milestone.description,
        sequenceOrder: milestone.sequenceOrder,
        status: milestone.status,
        plannedStartDate: new Date(milestone.plannedStartDate).toISOString().substring(0, 10),
        plannedEndDate: new Date(milestone.plannedEndDate).toISOString().substring(0, 10),
        actualStartDate: milestone.actualStartDate
          ? new Date(milestone.actualStartDate).toISOString().substring(0, 10)
          : undefined,
        actualEndDate: milestone.actualEndDate
          ? new Date(milestone.actualEndDate).toISOString().substring(0, 10)
          : undefined,
        completionPercentage: percentage,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadMilestones(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update progress.';
        },
      });
  }

  // ── Calculated values ──────────────────────────────────────────────────────

  calculateOverallProgress(): number {
    if (!this.milestones.length) return 0;
    const total = this.milestones.reduce((sum, m) => sum + m.completionPercentage, 0);
    return Math.round(total / this.milestones.length);
  }

  // ── Badge helpers ──────────────────────────────────────────────────────────

  getMilestoneStatusClass(status: MilestoneStatus): string {
    switch (status) {
      case MilestoneStatus.NotStarted:  return 'bg-secondary';
      case MilestoneStatus.InProgress:  return 'bg-primary';
      case MilestoneStatus.Completed:   return 'bg-success';
      case MilestoneStatus.Delayed:     return 'bg-warning text-dark';
      case MilestoneStatus.Cancelled:   return 'bg-danger';
      default:                          return 'bg-secondary';
    }
  }

  getMilestoneStatusLabel(status: MilestoneStatus): string {
    return MilestoneStatus[status] ?? 'Unknown';
  }

  getActionStatusClass(status: ActionItemStatus): string {
    switch (status) {
      case ActionItemStatus.NotStarted: return 'bg-secondary';
      case ActionItemStatus.InProgress: return 'bg-primary';
      case ActionItemStatus.Completed:  return 'bg-success';
      case ActionItemStatus.Deferred:   return 'bg-warning text-dark';
      case ActionItemStatus.Cancelled:  return 'bg-danger';
      default:                          return 'bg-secondary';
    }
  }

  getActionStatusLabel(status: ActionItemStatus): string {
    return ActionItemStatus[status] ?? 'Unknown';
  }

  getPriorityClass(priority: ActionItemPriority): string {
    switch (priority) {
      case ActionItemPriority.Critical: return 'bg-danger';
      case ActionItemPriority.High:     return 'bg-warning text-dark';
      case ActionItemPriority.Medium:   return 'bg-info text-dark';
      case ActionItemPriority.Low:      return 'bg-secondary';
      default:                          return 'bg-secondary';
    }
  }

  getPriorityLabel(priority: ActionItemPriority): string {
    return ActionItemPriority[priority] ?? 'Unknown';
  }

  getProgressClass(percentage: number): string {
    if (percentage >= 75) return 'bg-success';
    if (percentage >= 40) return 'bg-warning';
    return 'bg-danger';
  }

  // ── Form helpers ───────────────────────────────────────────────────────────

  hasError(field: string, error: string): boolean {
    const ctrl = this.newMilestoneForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.newMilestoneForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private buildNewMilestoneForm(): void {
    this.newMilestoneForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(300)]],
      description: [''],
      sequenceOrder: [1, [Validators.required, Validators.min(1)]],
      plannedStartDate: ['', [Validators.required]],
      plannedEndDate: ['', [Validators.required]],
    });
  }

  private loadMilestones(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.milestoneService
      .getByProject(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (milestones) => {
          const data = (milestones as any)?.data ?? milestones;
          this.milestones = (Array.isArray(data) ? data : [])
            .sort((a: MilestoneDetail, b: MilestoneDetail) => a.sequenceOrder - b.sequenceOrder);
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load milestones.';
          this.isLoading = false;
        },
      });
  }
}
