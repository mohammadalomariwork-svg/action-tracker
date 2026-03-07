import {
  Component,
  OnInit,
  OnChanges,
  SimpleChanges,
  Input,
  Output,
  EventEmitter,
  DestroyRef,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { MilestoneService } from '../../../services/milestone.service';
import {
  MilestoneDetail,
  MilestoneStatus,
} from '../../../models/project.models';

@Component({
  selector: 'app-milestone-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './milestone-form.component.html',
  styleUrl: './milestone-form.component.scss',
})
export class MilestoneFormComponent implements OnInit, OnChanges {
  @Input() milestone?: MilestoneDetail;
  @Input({ required: true }) projectId!: number;
  @Input() isBaselined = false;

  @Output() saved = new EventEmitter<MilestoneDetail>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly milestoneService = inject(MilestoneService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  form!: FormGroup;
  isSubmitting = false;
  errorMessage: string | null = null;

  readonly MilestoneStatus = MilestoneStatus;

  get isEditMode(): boolean {
    return !!this.milestone;
  }

  ngOnInit(): void {
    this.buildForm();
    if (this.milestone) {
      this.populateForm();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['milestone'] && this.form) {
      if (this.milestone) {
        this.populateForm();
      } else {
        this.form.reset();
      }
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;
    const v = this.form.getRawValue();

    if (this.isEditMode && this.milestone) {
      this.milestoneService
        .update(this.milestone.id, {
          id: this.milestone.id,
          title: v.title,
          description: v.description || undefined,
          sequenceOrder: v.sequenceOrder,
          status: v.status,
          plannedStartDate: v.plannedStartDate,
          plannedEndDate: v.plannedEndDate,
          actualStartDate: v.actualStartDate || undefined,
          actualEndDate: v.actualEndDate || undefined,
          completionPercentage: v.completionPercentage,
        })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (result) => {
            this.isSubmitting = false;
            this.saved.emit(result);
          },
          error: (err) => {
            this.errorMessage = err?.error?.message ?? 'Failed to update milestone.';
            this.isSubmitting = false;
          },
        });
    } else {
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
          next: (result) => {
            this.isSubmitting = false;
            this.saved.emit(result);
          },
          error: (err) => {
            this.errorMessage = err?.error?.message ?? 'Failed to create milestone.';
            this.isSubmitting = false;
          },
        });
    }
  }

  onCancel(): void {
    this.cancelled.emit();
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private helpers ────────────────────────────────────────────────────────

  private buildForm(): void {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(300)]],
      description: [''],
      sequenceOrder: [1, [Validators.required, Validators.min(1)]],
      plannedStartDate: ['', [Validators.required]],
      plannedEndDate: ['', [Validators.required]],
      status: [MilestoneStatus.NotStarted],
      completionPercentage: [0, [Validators.min(0), Validators.max(100)]],
      actualStartDate: [''],
      actualEndDate: [''],
    });
  }

  private populateForm(): void {
    if (!this.milestone) return;

    this.form.patchValue({
      title: this.milestone.title,
      description: this.milestone.description ?? '',
      sequenceOrder: this.milestone.sequenceOrder,
      plannedStartDate: this.milestone.plannedStartDate
        ? new Date(this.milestone.plannedStartDate).toISOString().substring(0, 10)
        : '',
      plannedEndDate: this.milestone.plannedEndDate
        ? new Date(this.milestone.plannedEndDate).toISOString().substring(0, 10)
        : '',
      status: this.milestone.status,
      completionPercentage: this.milestone.completionPercentage,
      actualStartDate: this.milestone.actualStartDate
        ? new Date(this.milestone.actualStartDate).toISOString().substring(0, 10)
        : '',
      actualEndDate: this.milestone.actualEndDate
        ? new Date(this.milestone.actualEndDate).toISOString().substring(0, 10)
        : '',
    });

    if (this.isBaselined) {
      this.form.get('plannedStartDate')!.disable();
      this.form.get('plannedEndDate')!.disable();
    }
  }
}
