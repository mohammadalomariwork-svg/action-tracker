import { Component, OnInit, DestroyRef, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectService } from '../../services/project.service';
import {
  ProjectType,
  ProjectStatus,
  ProjectPriority,
  ProjectResponse,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { AssignableUser } from '../../../../core/models/action-item.model';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('plannedStartDate')?.value;
  const end = group.get('plannedEndDate')?.value;
  if (start && end && end <= start) return { dateRange: true };
  return null;
}

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink, BreadcrumbComponent],
  templateUrl: './project-form.component.html',
  styleUrl: './project-form.component.scss',
})
export class ProjectFormComponent implements OnInit {
  private readonly fb            = inject(FormBuilder);
  private readonly projectSvc    = inject(ProjectService);
  private readonly route         = inject(ActivatedRoute);
  private readonly router        = inject(Router);
  private readonly destroyRef    = inject(DestroyRef);

  isEditMode = false;
  projectId: string | null = null;
  workspaceId!: string;
  isBaselined = false;

  strategicObjectives: StrategicObjectiveOption[] = [];
  availableUsers: AssignableUser[] = [];

  // Sponsor multi-select
  selectedSponsorIds: string[] = [];
  sponsorDropdownOpen = false;
  sponsorSearchTerm = '';

  isLoading = false;
  errorMessage: string | null = null;

  form!: FormGroup;

  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  readonly PRIORITY_OPTIONS = [
    { value: ProjectPriority.Low, label: 'Low' },
    { value: ProjectPriority.Medium, label: 'Medium' },
    { value: ProjectPriority.High, label: 'High' },
    { value: ProjectPriority.Critical, label: 'Critical' },
  ];

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.projectId = idParam;
    }

    const wsId = this.route.snapshot.queryParamMap.get('workspaceId');
    if (wsId) this.workspaceId = wsId;

    this.buildForm();
    this.loadUsers();

    if (this.isEditMode && this.projectId) {
      this.loadProject(this.projectId);
    }

    this.setupProjectTypeListener();
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  get isStrategic(): boolean {
    return this.form.get('projectType')?.value === ProjectType.Strategic;
  }

  get hasDateRangeError(): boolean {
    return !!(this.form.hasError('dateRange') && this.form.get('plannedEndDate')?.touched);
  }

  // ── Sponsor multi-select ──────────────────────────
  get filteredSponsorUsers(): AssignableUser[] {
    if (!this.sponsorSearchTerm.trim()) return this.availableUsers;
    const term = this.sponsorSearchTerm.toLowerCase();
    return this.availableUsers.filter(u => u.fullName.toLowerCase().includes(term));
  }

  toggleSponsor(userId: string): void {
    const idx = this.selectedSponsorIds.indexOf(userId);
    if (idx >= 0) this.selectedSponsorIds.splice(idx, 1);
    else this.selectedSponsorIds.push(userId);
  }

  isSponsorSelected(userId: string): boolean {
    return this.selectedSponsorIds.includes(userId);
  }

  getSponsorName(userId: string): string {
    return this.availableUsers.find(u => u.id === userId)?.fullName ?? userId;
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.sponsorDropdownOpen = false;
  }

  onSubmit(): void {
    if (this.form.invalid || this.selectedSponsorIds.length === 0) {
      this.form.markAllAsTouched();
      if (this.selectedSponsorIds.length === 0)
        this.errorMessage = 'At least one sponsor is required.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;
    const v = this.form.getRawValue();

    if (this.isEditMode && this.projectId) {
      this.projectSvc.update(this.projectId, {
        name: v.name,
        description: v.description || undefined,
        projectType: v.projectType,
        status: v.status,
        strategicObjectiveId: v.strategicObjectiveId || undefined,
        priority: v.priority,
        projectManagerUserId: v.projectManagerUserId,
        sponsorUserIds: this.selectedSponsorIds,
        plannedStartDate: v.plannedStartDate,
        plannedEndDate: v.plannedEndDate,
        actualStartDate: v.actualStartDate || undefined,
        approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.isLoading = false;
          this.router.navigate(['/workspaces', this.workspaceId]);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update project.';
          this.isLoading = false;
        },
      });
    } else {
      this.projectSvc.create({
        name: v.name,
        description: v.description || undefined,
        workspaceId: this.workspaceId,
        projectType: v.projectType,
        strategicObjectiveId: v.strategicObjectiveId || undefined,
        priority: v.priority,
        projectManagerUserId: v.projectManagerUserId,
        sponsorUserIds: this.selectedSponsorIds,
        plannedStartDate: v.plannedStartDate,
        plannedEndDate: v.plannedEndDate,
        approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.isLoading = false;
          this.router.navigate(['/workspaces', this.workspaceId]);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to create project.';
          this.isLoading = false;
        },
      });
    }
  }

  onCancel(): void {
    if (this.workspaceId) this.router.navigate(['/workspaces', this.workspaceId]);
    else this.router.navigate(['/workspaces']);
  }

  private buildForm(): void {
    this.form = this.fb.group(
      {
        name: ['', [Validators.required, Validators.maxLength(255)]],
        description: [''],
        projectType: [ProjectType.Operational, [Validators.required]],
        strategicObjectiveId: [null as string | null],
        priority: [ProjectPriority.Medium, [Validators.required]],
        projectManagerUserId: ['', [Validators.required]],
        plannedStartDate: ['', [Validators.required]],
        plannedEndDate: ['', [Validators.required]],
        approvedBudget: [null as number | null],
        status: [ProjectStatus.Draft],
        actualStartDate: [''],
      },
      { validators: dateRangeValidator }
    );
  }

  private setupProjectTypeListener(): void {
    this.form.get('projectType')!.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((type: ProjectType) => {
        const soCtrl = this.form.get('strategicObjectiveId')!;
        if (type === ProjectType.Strategic) {
          soCtrl.setValidators([Validators.required]);
          this.loadStrategicObjectives();
        } else {
          soCtrl.clearValidators();
          soCtrl.setValue(null);
          this.strategicObjectives = [];
        }
        soCtrl.updateValueAndValidity();
      });
  }

  private loadUsers(): void {
    this.projectSvc.getAssignableUsers()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.availableUsers = res.data ?? [],
        error: () => this.errorMessage = 'Failed to load users.',
      });
  }

  private loadStrategicObjectives(): void {
    if (!this.workspaceId) return;
    this.projectSvc.getStrategicObjectivesForWorkspace(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.strategicObjectives = (res.data ?? []) as StrategicObjectiveOption[],
        error: () => this.strategicObjectives = [],
      });
  }

  private loadProject(id: string): void {
    this.isLoading = true;
    this.projectSvc.getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const p: ProjectResponse = res.data;
          this.workspaceId = p.workspaceId;
          this.isBaselined = p.isBaselined;
          this.selectedSponsorIds = p.sponsors.map(s => s.userId);

          this.form.patchValue({
            name: p.name,
            description: p.description ?? '',
            projectType: p.projectType,
            strategicObjectiveId: p.strategicObjectiveId ?? null,
            priority: p.priority,
            projectManagerUserId: p.projectManagerUserId,
            plannedStartDate: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : '',
            plannedEndDate: p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : '',
            approvedBudget: p.approvedBudget ?? null,
            status: p.status,
            actualStartDate: p.actualStartDate ? String(p.actualStartDate).substring(0, 10) : '',
          });

          if (this.isBaselined) {
            this.form.get('plannedStartDate')!.disable();
            this.form.get('plannedEndDate')!.disable();
          }

          if (p.projectType === ProjectType.Strategic) {
            this.loadStrategicObjectives();
          }

          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project.';
          this.isLoading = false;
        },
      });
  }
}
