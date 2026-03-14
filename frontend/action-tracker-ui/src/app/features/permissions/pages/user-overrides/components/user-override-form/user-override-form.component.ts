import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  OnDestroy,
  ViewChild,
  inject,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  OrgUnitScope,
  PermissionAction,
  PermissionArea,
  PERMISSION_ACTION_LABELS,
  PERMISSION_AREA_LABELS,
  ORG_UNIT_SCOPE_LABELS,
} from '../../../../models/permission.enums';
import { UserPermissionOverrideDto } from '../../../../models/user-permission.model';
import { UserPermissionService } from '../../../../services/user-permission.service';

declare const bootstrap: { Modal: new (el: HTMLElement, opts?: object) => { show(): void; hide(): void; dispose(): void } };

type FormMode = 'create' | 'edit';

interface OverrideForm {
  area:         FormControl<number>;
  action:       FormControl<number>;
  isGranted:    FormControl<boolean>;
  orgUnitScope: FormControl<number>;
  orgUnitId:    FormControl<string>;
  orgUnitName:  FormControl<string>;
  reason:       FormControl<string>;
  expiresAt:    FormControl<string>;
  isActive:     FormControl<boolean>;
}

@Component({
  selector: 'app-user-override-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './user-override-form.component.html',
  styleUrl: './user-override-form.component.scss',
})
export class UserOverrideFormComponent implements AfterViewInit, OnDestroy {
  private readonly svc       = inject(UserPermissionService);
  private readonly fb        = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('modalEl') modalEl!: ElementRef<HTMLElement>;
  private bsModal!: ReturnType<typeof bootstrap.Modal>;

  private userId          = '';
  private userDisplayName = '';
  private editOverrideId  = '';

  readonly saved = output<void>();

  mode    = signal<FormMode>('create');
  saving  = signal(false);
  error   = signal<string | null>(null);

  // ── Select options ────────────────────────────────────────────────────────
  readonly areaEntries = (Object.values(PermissionArea).filter(
    (v): v is PermissionArea => typeof v === 'number'
  )).map(v => ({ value: v, label: PERMISSION_AREA_LABELS[v] }));

  readonly actionEntries = (Object.values(PermissionAction).filter(
    (v): v is PermissionAction => typeof v === 'number'
  )).map(v => ({ value: v, label: PERMISSION_ACTION_LABELS[v] }));

  readonly scopeEntries = (Object.values(OrgUnitScope).filter(
    (v): v is OrgUnitScope => typeof v === 'number'
  )).map(v => ({ value: v, label: ORG_UNIT_SCOPE_LABELS[v] }));

  readonly SpecificOrgUnit = OrgUnitScope.SpecificOrgUnit;

  // ── Form ──────────────────────────────────────────────────────────────────
  readonly form: FormGroup<OverrideForm> = this.fb.group({
    area:         this.fb.nonNullable.control(0, Validators.required),
    action:       this.fb.nonNullable.control(0, Validators.required),
    isGranted:    this.fb.nonNullable.control(true),
    orgUnitScope: this.fb.nonNullable.control(0),
    orgUnitId:    this.fb.nonNullable.control(''),
    orgUnitName:  this.fb.nonNullable.control(''),
    reason:       this.fb.nonNullable.control(''),
    expiresAt:    this.fb.nonNullable.control(''),
    isActive:     this.fb.nonNullable.control(true),
  } satisfies OverrideForm) as FormGroup<OverrideForm>;

  get isSpecificOrgUnit(): boolean {
    return Number(this.form.controls.orgUnitScope.value) === OrgUnitScope.SpecificOrgUnit;
  }

  get modalTitle(): string {
    return this.mode() === 'create' ? 'Add Permission Override' : 'Edit Permission Override';
  }

  ngAfterViewInit(): void {
    this.bsModal = new bootstrap.Modal(this.modalEl.nativeElement, { backdrop: 'static' });
  }

  ngOnDestroy(): void {
    this.bsModal?.dispose();
  }

  // ── Open methods ──────────────────────────────────────────────────────────

  openCreate(userId: string, displayName: string): void {
    this.userId          = userId;
    this.userDisplayName = displayName;
    this.editOverrideId  = '';
    this.mode.set('create');
    this.error.set(null);
    this.form.reset({ area: 0, action: 0, isGranted: true, orgUnitScope: 0,
                      orgUnitId: '', orgUnitName: '', reason: '', expiresAt: '', isActive: true });
    this.form.controls.area.enable();
    this.form.controls.action.enable();
    this.bsModal.show();
  }

  openEdit(override: UserPermissionOverrideDto): void {
    this.userId          = override.userId;
    this.userDisplayName = override.userDisplayName;
    this.editOverrideId  = override.id;
    this.mode.set('edit');
    this.error.set(null);
    this.form.reset({
      area:         this.labelToAreaEnum(override.area)         ?? 0,
      action:       this.labelToActionEnum(override.action)     ?? 0,
      isGranted:    override.isGranted,
      orgUnitScope: this.labelToScopeEnum(override.orgUnitScope) ?? 0,
      orgUnitId:    override.orgUnitId   ?? '',
      orgUnitName:  override.orgUnitName ?? '',
      reason:       override.reason      ?? '',
      expiresAt:    override.expiresAt   ? override.expiresAt.slice(0, 16) : '',
      isActive:     override.isActive,
    });
    this.form.controls.area.disable();
    this.form.controls.action.disable();
    this.bsModal.show();
  }

  close(): void {
    this.bsModal?.hide();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);

    const obs$ = this.mode() === 'create'
      ? this.svc.createOverride({
          userId:          this.userId,
          userDisplayName: this.userDisplayName,
          area:            v.area   + 1,   // frontend 0-based → backend 1-based
          action:          v.action + 1,
          isGranted:       v.isGranted,
          orgUnitScope:    v.orgUnitScope + 1,
          orgUnitId:       v.orgUnitId   || undefined,
          orgUnitName:     v.orgUnitName || undefined,
          reason:          v.reason      || undefined,
          expiresAt:       v.expiresAt   || undefined,
        })
      : this.svc.updateOverride(this.editOverrideId, {
          isGranted:    v.isGranted,
          orgUnitScope: v.orgUnitScope + 1,
          orgUnitId:    v.orgUnitId   || undefined,
          orgUnitName:  v.orgUnitName || undefined,
          reason:       v.reason      || undefined,
          expiresAt:    v.expiresAt   || undefined,
          isActive:     v.isActive,
        });

    obs$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next:  () => { this.saving.set(false); this.close(); this.saved.emit(); },
      error: (err) => { this.saving.set(false); this.error.set(err?.error?.message ?? 'Failed to save override.'); },
    });
  }

  // ── Display helpers ───────────────────────────────────────────────────────

  getLabelForArea(value: number): string {
    return PERMISSION_AREA_LABELS[value as PermissionArea] ?? String(value);
  }

  getLabelForAction(value: number): string {
    return PERMISSION_ACTION_LABELS[value as PermissionAction] ?? String(value);
  }

  // ── Label → enum helpers (string from backend → frontend 0-based enum) ───

  private labelToAreaEnum(label: string): PermissionArea | null {
    const e = (Object.entries(PERMISSION_AREA_LABELS) as [string, string][]).find(([, v]) => v === label);
    return e ? Number(e[0]) as PermissionArea : null;
  }

  private labelToActionEnum(label: string): PermissionAction | null {
    const e = (Object.entries(PERMISSION_ACTION_LABELS) as [string, string][]).find(([, v]) => v === label);
    return e ? Number(e[0]) as PermissionAction : null;
  }

  private labelToScopeEnum(label: string): OrgUnitScope | null {
    const e = (Object.entries(ORG_UNIT_SCOPE_LABELS) as [string, string][]).find(([, v]) => v === label);
    return e ? Number(e[0]) as OrgUnitScope : null;
  }
}
