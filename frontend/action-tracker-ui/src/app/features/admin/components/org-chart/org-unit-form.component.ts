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

import { OrgUnitService } from '../../services/org-unit.service';
import { OrgUnit, OrgUnitTree } from '../../models/org-chart.models';

@Component({
  selector: 'app-org-unit-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './org-unit-form.component.html',
  styleUrl: './org-unit-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrgUnitFormComponent implements OnInit, OnChanges {
  @Input() mode: 'add' | 'edit' = 'add';
  @Input() parentUnit: OrgUnitTree | null = null;
  @Input() existingUnit: OrgUnitTree | null = null;

  @Output() saved     = new EventEmitter<OrgUnit>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly fb             = inject(FormBuilder);
  private readonly orgUnitService = inject(OrgUnitService);
  private readonly destroyRef     = inject(DestroyRef);

  readonly submitting = signal(false);
  readonly error      = signal<string | null>(null);

  form!: FormGroup;

  get computedLevel(): number {
    if (this.mode === 'add') {
      return this.parentUnit ? this.parentUnit.level + 1 : 1;
    }
    return this.existingUnit?.level ?? 1;
  }

  get exceedsMaxDepth(): boolean {
    return this.computedLevel > 10;
  }

  get parentLabel(): string {
    if (this.mode === 'add') {
      return this.parentUnit ? `${this.parentUnit.name} (Level ${this.parentUnit.level})` : 'Root unit (KU)';
    }
    // edit: existingUnit's parent info not directly available in OrgUnitTree, show level
    return this.existingUnit ? `Level ${this.existingUnit.level}` : '—';
  }

  ngOnInit(): void {
    this.buildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['existingUnit'] || changes['mode']) && this.form) {
      this.patchForm();
    }
  }

  private buildForm(): void {
    this.form = this.fb.group({
      name: [
        '',
        [Validators.required, Validators.minLength(2), Validators.maxLength(200)],
      ],
      code: [
        '',
        [Validators.maxLength(50), Validators.pattern(/^[A-Z0-9]*$/)],
      ],
      description: ['', [Validators.maxLength(500)]],
    });

    this.patchForm();
  }

  private patchForm(): void {
    if (this.mode === 'edit' && this.existingUnit) {
      this.form.patchValue({
        name:        this.existingUnit.name        ?? '',
        code:        this.existingUnit.code        ?? '',
        description: this.existingUnit.description ?? '',
      });
    } else {
      this.form.reset({ name: '', code: '', description: '' });
    }
    this.error.set(null);
  }

  submit(): void {
    if (this.form.invalid || this.exceedsMaxDepth) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, code, description } = this.form.value as {
      name: string;
      code: string;
      description: string;
    };

    this.submitting.set(true);
    this.error.set(null);

    const request$ =
      this.mode === 'add'
        ? this.orgUnitService.create({
            name,
            code:        code        || undefined,
            description: description || undefined,
            parentId:    this.parentUnit?.id,
          })
        : this.orgUnitService.update(this.existingUnit!.id, {
            name,
            code:        code        || undefined,
            description: description || undefined,
            parentId:    this.existingUnit!.parentId,
          });

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (unit) => {
        this.submitting.set(false);
        this.saved.emit(unit);
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

  // Template helpers for validation messages
  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }
}
