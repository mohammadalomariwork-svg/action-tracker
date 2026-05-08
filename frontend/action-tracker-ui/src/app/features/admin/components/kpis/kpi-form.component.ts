import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnChanges,
  SimpleChanges,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { KpiService }                from '../../services/kpi.service';
import { StrategicObjectiveService } from '../../services/strategic-objective.service';
import { Kpi, MeasurementPeriod, MeasurementPeriodLabels } from '../../models/kpi.models';
import { StrategicObjective }        from '../../models/strategic-objectives.models';

@Component({
  selector: 'app-kpi-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './kpi-form.component.html',
  styleUrl: './kpi-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KpiFormComponent implements OnInit, OnChanges {
  @Input() existingKpi: Kpi | null = null;
  @Input() preselectedObjectiveId: string | null = null;
  /**
   * When set (in create mode), the objective dropdown is **locked** to this single
   * objective: only that objective is fetched (via `getById`) and rendered as the
   * only option, and the form control is disabled. Used by the per-objective
   * Strategy view, where the KPI must belong to the objective the user is on.
   */
  @Input() restrictToObjectiveId: string | null = null;

  @Output() saved     = new EventEmitter<Kpi>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly kpiService       = inject(KpiService);
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly fb               = inject(FormBuilder);
  private readonly destroyRef       = inject(DestroyRef);

  readonly objectives  = signal<StrategicObjective[]>([]);
  readonly submitting  = signal(false);
  readonly error       = signal<string | null>(null);

  readonly PeriodLabels = MeasurementPeriodLabels;
  readonly periodOptions: { value: MeasurementPeriod; label: string }[] = [
    { value: 1, label: 'Monthly' },
    { value: 2, label: 'Quarterly' },
    { value: 3, label: 'Semi-Annual' },
    { value: 4, label: 'Yearly' },
  ];

  form!: FormGroup;

  get isEditMode(): boolean { return !!this.existingKpi; }

  ngOnInit(): void {
    this.buildForm();
    this.loadObjectives();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['existingKpi'] || changes['preselectedObjectiveId']) && this.form) {
      this.patchForm();
    }
  }

  private buildForm(): void {
    this.form = this.fb.group({
      strategicObjectiveId: ['', [Validators.required]],
      name:                 ['', [Validators.required, Validators.maxLength(300)]],
      description:          ['', [Validators.required, Validators.maxLength(1000)]],
      calculationMethod:    ['', [Validators.required, Validators.maxLength(500)]],
      period:               [null as MeasurementPeriod | null, [Validators.required]],
      unit:                 ['', [Validators.maxLength(50)]],
    });
    this.patchForm();
  }

  private patchForm(): void {
    if (this.existingKpi) {
      this.form.patchValue({
        strategicObjectiveId: this.existingKpi.strategicObjectiveId,
        name:                 this.existingKpi.name,
        description:          this.existingKpi.description,
        calculationMethod:    this.existingKpi.calculationMethod,
        period:               this.existingKpi.periodValue,
        unit:                 this.existingKpi.unit ?? '',
      });
      // Objective cannot change on edit
      this.form.get('strategicObjectiveId')?.disable();
    } else {
      this.form.reset({
        strategicObjectiveId:
          this.restrictToObjectiveId ?? this.preselectedObjectiveId ?? '',
        name: '',
        description: '',
        calculationMethod: '',
        period: null,
        unit: '',
      });
      // Lock the field when the parent restricts the form to a single objective;
      // otherwise allow the user to pick from the loaded list.
      if (this.restrictToObjectiveId) {
        this.form.get('strategicObjectiveId')?.disable();
      } else {
        this.form.get('strategicObjectiveId')?.enable();
      }
    }
    this.error.set(null);
  }

  private loadObjectives(): void {
    // Restricted mode (create only): fetch just the single allowed objective so
    // the dropdown shows only that one option. The control is already disabled
    // by patchForm; we only need to populate the option list and ensure the
    // value matches.
    if (this.restrictToObjectiveId && !this.isEditMode) {
      this.objectiveService
        .getById(this.restrictToObjectiveId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (obj) => {
            this.objectives.set([obj]);
            this.form.get('strategicObjectiveId')?.setValue(obj.id);
            this.form.get('strategicObjectiveId')?.disable();
          },
          error: () => this.error.set('Failed to load strategic objective.'),
        });
      return;
    }

    this.objectiveService
      .getAll(1, 200)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.objectives.set(res.objectives.filter((o) => !o.isDeleted));
          // patch objective if preselected and form already built
          if (!this.isEditMode && this.preselectedObjectiveId) {
            this.form.get('strategicObjectiveId')?.setValue(this.preselectedObjectiveId);
          }
        },
        error: () => this.error.set('Failed to load strategic objectives.'),
      });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue() as {
      strategicObjectiveId: string;
      name: string;
      description: string;
      calculationMethod: string;
      period: MeasurementPeriod;
      unit: string;
    };

    this.submitting.set(true);
    this.error.set(null);

    const request$ = this.isEditMode
      ? this.kpiService.update(this.existingKpi!.id, {
          name: raw.name,
          description: raw.description,
          calculationMethod: raw.calculationMethod,
          period: raw.period,
          unit: raw.unit || undefined,
        })
      : this.kpiService.create({
          strategicObjectiveId: raw.strategicObjectiveId,
          name: raw.name,
          description: raw.description,
          calculationMethod: raw.calculationMethod,
          period: raw.period,
          unit: raw.unit || undefined,
        });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (kpi) => {
        this.submitting.set(false);
        this.saved.emit(kpi);
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(err?.error?.message ?? 'An error occurred. Please try again.');
      },
    });
  }

  cancel(): void { this.cancelled.emit(); }

  hasError(field: string, errorKey: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(errorKey));
  }

  charCount(field: string): number {
    return this.form.get(field)?.value?.length ?? 0;
  }
}
