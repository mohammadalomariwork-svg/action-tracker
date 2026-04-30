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

import { StrategicObjectiveService } from '../../services/strategic-objective.service';
import { OrgUnitService }            from '../../services/org-unit.service';
import { StrategicObjective }        from '../../models/strategic-objectives.models';
import { OrgUnit }                   from '../../models/org-chart.models';
import { OrgUnitSelectComponent }    from '../../../../shared';

@Component({
  selector: 'app-objective-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, OrgUnitSelectComponent],
  templateUrl: './objective-form.component.html',
  styleUrl: './objective-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ObjectiveFormComponent implements OnInit, OnChanges {
  @Input() existingObjective: StrategicObjective | null = null;

  @Output() saved     = new EventEmitter<StrategicObjective>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly orgUnitService   = inject(OrgUnitService);
  private readonly fb               = inject(FormBuilder);
  private readonly destroyRef       = inject(DestroyRef);

  readonly orgUnits   = signal<OrgUnit[]>([]);
  readonly submitting = signal(false);
  readonly error      = signal<string | null>(null);

  form!: FormGroup;

  get isEditMode(): boolean {
    return !!this.existingObjective;
  }

  /** Org units grouped by level for display */
  readonly levelGroups = signal<Map<number, OrgUnit[]>>(new Map());

  ngOnInit(): void {
    this.buildForm();
    this.loadOrgUnits();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['existingObjective'] && this.form) {
      this.patchForm();
    }
  }

  private buildForm(): void {
    this.form = this.fb.group({
      orgUnitId:   ['', [Validators.required]],
      statement:   ['', [Validators.required, Validators.maxLength(300)]],
      description: ['', [Validators.required, Validators.maxLength(1000)]],
    });
    this.patchForm();
  }

  private patchForm(): void {
    if (this.existingObjective) {
      this.form.patchValue({
        orgUnitId:   this.existingObjective.orgUnitId,
        statement:   this.existingObjective.statement,
        description: this.existingObjective.description,
      });
    } else {
      this.form.reset({ orgUnitId: '', statement: '', description: '' });
    }
    this.error.set(null);
  }

  private loadOrgUnits(): void {
    this.orgUnitService
      .getAll(1, 200)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const units = res.orgUnits.sort((a, b) => a.level - b.level || a.name.localeCompare(b.name));
          this.orgUnits.set(units);

          // Build level groups for optgroup rendering
          const map = new Map<number, OrgUnit[]>();
          for (const u of units) {
            if (!map.has(u.level)) map.set(u.level, []);
            map.get(u.level)!.push(u);
          }
          this.levelGroups.set(map);
        },
        error: () => this.error.set('Failed to load org units.'),
      });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { orgUnitId, statement, description } = this.form.value as {
      orgUnitId: string;
      statement: string;
      description: string;
    };

    this.submitting.set(true);
    this.error.set(null);

    const request$ = this.isEditMode
      ? this.objectiveService.update(this.existingObjective!.id, { orgUnitId, statement, description })
      : this.objectiveService.create({ orgUnitId, statement, description });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (obj) => {
        this.submitting.set(false);
        this.saved.emit(obj);
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(err?.error?.message ?? 'An error occurred. Please try again.');
      },
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  levelLabel(level: number): string {
    return `Level ${level}`;
  }

  orgUnitLabel(u: OrgUnit): string {
    const code = u.code ? `[${u.code}] ` : '';
    return `${code}${u.name} (Level ${u.level})`;
  }
}
