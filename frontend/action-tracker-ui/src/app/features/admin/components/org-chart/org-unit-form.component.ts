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
import { OrgUnitSelectComponent } from '../../../../shared';

@Component({
  selector: 'app-org-unit-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, OrgUnitSelectComponent],
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

  readonly submitting          = signal(false);
  readonly error               = signal<string | null>(null);
  readonly allUnits            = signal<OrgUnit[]>([]);
  readonly loadingUnits        = signal(false);
  /** Level of the currently selected parent (null = no parent = root). */
  readonly selectedParentLevel = signal<number | null>(null);

  form!: FormGroup;

  get computedLevel(): number {
    const pl = this.selectedParentLevel();
    return pl !== null ? pl + 1 : 1;
  }

  get exceedsMaxDepth(): boolean {
    return this.computedLevel > 10;
  }

  ngOnInit(): void {
    this.buildForm();
    this.loadUnits();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['existingUnit'] || changes['mode'] || changes['parentUnit']) && this.form) {
      this.patchForm();
    }
  }

  private loadUnits(): void {
    this.loadingUnits.set(true);
    this.orgUnitService
      .getAll(1, 1000, false)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.allUnits.set(res.orgUnits);
          this.loadingUnits.set(false);
          // Re-sync level in case units loaded after patchForm ran
          const parentId = this.form.get('parentId')?.value as string;
          const parent   = res.orgUnits.find(u => u.id === parentId);
          this.selectedParentLevel.set(parent?.level ?? null);
        },
        error: () => this.loadingUnits.set(false),
      });
  }

  /** Options for the parent dropdown, excluding the unit being edited. */
  get parentOptions(): OrgUnit[] {
    const editingId = this.mode === 'edit' ? this.existingUnit?.id : null;
    return this.allUnits().filter(u => u.id !== editingId);
  }

  private buildForm(): void {
    const initialParentId = this.mode === 'edit'
      ? (this.existingUnit?.parentId ?? '')
      : (this.parentUnit?.id ?? '');

    this.form = this.fb.group({
      name:        ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
      parentId:    [initialParentId],
      description: ['', [Validators.maxLength(500)]],
    });

    // Keep selectedParentLevel in sync so computedLevel updates reactively
    this.form.get('parentId')!.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((id: string) => {
        const parent = this.allUnits().find(u => u.id === id);
        this.selectedParentLevel.set(parent?.level ?? null);
      });

    this.patchForm();
  }

  private patchForm(): void {
    if (!this.form) return;

    if (this.mode === 'edit' && this.existingUnit) {
      const parentId = this.existingUnit.parentId ?? '';
      this.form.patchValue({ name: this.existingUnit.name ?? '', description: this.existingUnit.description ?? '', parentId });
      const parent = this.allUnits().find(u => u.id === parentId);
      this.selectedParentLevel.set(parent?.level ?? null);
    } else {
      const parentId = this.parentUnit?.id ?? '';
      this.form.reset({ name: '', description: '', parentId });
      this.selectedParentLevel.set(this.parentUnit?.level ?? null);
    }
    this.error.set(null);
  }

  submit(): void {
    if (this.form.invalid || this.exceedsMaxDepth) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, description, parentId } = this.form.value as {
      name: string;
      description: string;
      parentId: string;
    };

    this.submitting.set(true);
    this.error.set(null);

    const request$ =
      this.mode === 'add'
        ? this.orgUnitService.create({
            name,
            description: description || undefined,
            parentId:    parentId    || undefined,
          })
        : this.orgUnitService.update(this.existingUnit!.id, {
            name,
            description: description || undefined,
            parentId:    parentId    || undefined,
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

  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  /** Label shown in the dropdown (indented by level using em dashes). */
  unitLabel(unit: OrgUnit): string {
    const indent = '— '.repeat(unit.level - 1);
    return `${indent}${unit.name}${unit.code ? ' (' + unit.code + ')' : ''}`;
  }
}
