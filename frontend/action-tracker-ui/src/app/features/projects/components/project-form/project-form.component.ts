import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectService } from '../../services/project.service';
import { StrategicObjectiveService } from '../../services/strategic-objective.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { UserService } from '../../../../core/services/user.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  ProjectType,
  ProjectStatus,
  StrategicObjective,
  ProjectDetail,
} from '../../models/project.models';
import { UserProfile } from '../../../../core/models/user.model';

/**
 * Cross-field validator: plannedEndDate must be after plannedStartDate.
 */
function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('plannedStartDate')?.value;
  const end = group.get('plannedEndDate')?.value;
  if (start && end && end <= start) {
    return { dateRange: true };
  }
  return null;
}

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './project-form.component.html',
  styleUrl: './project-form.component.scss',
})
export class ProjectFormComponent implements OnInit {
  private readonly fb                      = inject(FormBuilder);
  private readonly projectService          = inject(ProjectService);
  private readonly strategicObjService     = inject(StrategicObjectiveService);
  private readonly workspaceService        = inject(WorkspaceService);
  private readonly userService             = inject(UserService);
  private readonly authService             = inject(AuthService);
  private readonly route                   = inject(ActivatedRoute);
  private readonly router                  = inject(Router);
  private readonly destroyRef              = inject(DestroyRef);

  isEditMode = false;
  projectId: number | null = null;
  workspaceId!: string;
  workspaceOrgUnit = '';
  isBaselined = false;

  strategicObjectives: StrategicObjective[] = [];
  availableUsers: UserProfile[] = [];

  isLoading = false;
  errorMessage: string | null = null;

  form!: FormGroup;

  // Expose enums to template
  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.projectId = +idParam;
    }

    const wsId = this.route.snapshot.queryParamMap.get('workspaceId');
    if (wsId) {
      this.workspaceId = wsId;
    }

    this.buildForm();
    this.loadUsers();

    if (this.isEditMode && this.projectId !== null) {
      this.loadProject(this.projectId);
    } else if (this.workspaceId) {
      this.loadWorkspaceOrgUnit(this.workspaceId);
    }

    this.setupProjectTypeListener();
    this.setupUserNameAutoFill();
  }

  // ── Form helpers ────────────────────────────────────────────────────────────

  /** Returns true when the given field is touched and has the given error. */
  hasError(field: string, error: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  /** Returns true when the given field is touched and invalid. */
  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  /** Whether the project type is Strategic. */
  get isStrategic(): boolean {
    return this.form.get('projectType')?.value === ProjectType.Strategic;
  }

  /** Whether the date range cross-field validator has fired. */
  get hasDateRangeError(): boolean {
    return !!(this.form.hasError('dateRange') && this.form.get('plannedEndDate')?.touched);
  }

  // ── Actions ─────────────────────────────────────────────────────────────────

  /** Submits the form — creates or updates depending on mode. */
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    const v = this.form.getRawValue();

    if (this.isEditMode && this.projectId !== null) {
      this.projectService
        .update(this.projectId, {
          id: this.projectId,
          title: v.title,
          description: v.description || undefined,
          projectType: v.projectType,
          status: v.status,
          strategicObjectiveId: v.strategicObjectiveId || undefined,
          sponsorUserId: v.sponsorUserId,
          sponsorUserName: v.sponsorUserName,
          projectManagerUserId: v.projectManagerUserId,
          projectManagerUserName: v.projectManagerUserName,
          plannedStartDate: v.plannedStartDate,
          plannedEndDate: v.plannedEndDate,
          actualStartDate: v.actualStartDate || undefined,
          actualEndDate: v.actualEndDate || undefined,
        })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res) => {
            this.isLoading = false;
            this.router.navigate(['/projects', this.projectId]);
          },
          error: (err) => {
            this.errorMessage = err?.error?.message ?? 'Failed to update project.';
            this.isLoading = false;
          },
        });
    } else {
      this.authService.currentUser$
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(user => {
          this.projectService
            .create({
              workspaceId: this.workspaceId,
              title: v.title,
              description: v.description || undefined,
              projectType: v.projectType,
              strategicObjectiveId: v.strategicObjectiveId || undefined,
              sponsorUserId: v.sponsorUserId,
              sponsorUserName: v.sponsorUserName,
              projectManagerUserId: v.projectManagerUserId,
              projectManagerUserName: v.projectManagerUserName,
              plannedStartDate: v.plannedStartDate,
              plannedEndDate: v.plannedEndDate,
              createdByUserId: user?.email ?? '',
            })
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (res) => {
                this.isLoading = false;
                this.router.navigate(['/projects', (res as any)?.id ?? (res as any)?.data?.id]);
              },
              error: (err) => {
                this.errorMessage = err?.error?.message ?? 'Failed to create project.';
                this.isLoading = false;
              },
            });
        });
    }
  }

  /** Navigates back to the workspace detail page. */
  onCancel(): void {
    if (this.workspaceId) {
      this.router.navigate(['/workspaces', this.workspaceId]);
    } else {
      this.router.navigate(['/workspaces']);
    }
  }

  // ── Private helpers ─────────────────────────────────────────────────────────

  private buildForm(): void {
    this.form = this.fb.group(
      {
        title: ['', [Validators.required, Validators.maxLength(300)]],
        description: [''],
        projectType: [ProjectType.Operational, [Validators.required]],
        strategicObjectiveId: [null as number | null],
        sponsorUserId: ['', [Validators.required]],
        sponsorUserName: [''],
        projectManagerUserId: ['', [Validators.required]],
        projectManagerUserName: [''],
        plannedStartDate: ['', [Validators.required]],
        plannedEndDate: ['', [Validators.required]],
        status: [ProjectStatus.Draft],
        actualStartDate: [''],
        actualEndDate: [''],
      },
      { validators: dateRangeValidator }
    );
  }

  /** Listens for projectType changes to toggle strategic objective requirement. */
  private setupProjectTypeListener(): void {
    this.form
      .get('projectType')!
      .valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
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

  /** Auto-fills userName fields when userId dropdowns change. */
  private setupUserNameAutoFill(): void {
    this.form
      .get('sponsorUserId')!
      .valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((userId: string) => {
        const user = this.availableUsers.find(u => u.id === userId);
        this.form.get('sponsorUserName')!.setValue(user?.fullName ?? '');
      });

    this.form
      .get('projectManagerUserId')!
      .valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((userId: string) => {
        const user = this.availableUsers.find(u => u.id === userId);
        this.form.get('projectManagerUserName')!.setValue(user?.fullName ?? '');
      });
  }

  /** Loads all active users, filtering by workspace org unit when available. */
  private loadUsers(): void {
    this.userService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const all = res.data ?? [];
          if (this.workspaceOrgUnit) {
            this.availableUsers = all.filter(
              u => u.department === this.workspaceOrgUnit && u.isActive
            );
          } else {
            this.availableUsers = all.filter(u => u.isActive);
          }
        },
        error: () => {
          this.errorMessage = 'Failed to load users.';
        },
      });
  }

  /** Loads workspace org unit for filtering strategic objectives and users. */
  private loadWorkspaceOrgUnit(workspaceId: string): void {
    this.workspaceService
      .getWorkspaceById(workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspaceOrgUnit = res.data?.organizationUnit ?? '';
          this.filterUsersByOrgUnit();
          if (this.isStrategic) {
            this.loadStrategicObjectives();
          }
        },
        error: () => {
          this.errorMessage = 'Failed to load workspace info.';
        },
      });
  }

  /** Loads the project for edit mode. */
  private loadProject(id: number): void {
    this.isLoading = true;

    this.projectService
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project: ProjectDetail) => {
          const p = (project as any)?.data ?? project;
          this.workspaceId = p.workspaceId;
          this.isBaselined = p.isBaselined ?? false;

          this.form.patchValue({
            title: p.title,
            description: p.description ?? '',
            projectType: p.projectType,
            strategicObjectiveId: p.strategicObjectiveId ?? null,
            sponsorUserId: p.sponsorUserId,
            sponsorUserName: p.sponsorUserName,
            projectManagerUserId: p.projectManagerUserId,
            projectManagerUserName: p.projectManagerUserName,
            plannedStartDate: p.plannedStartDate
              ? new Date(p.plannedStartDate).toISOString().substring(0, 10)
              : '',
            plannedEndDate: p.plannedEndDate
              ? new Date(p.plannedEndDate).toISOString().substring(0, 10)
              : '',
            status: p.status,
            actualStartDate: p.actualStartDate
              ? new Date(p.actualStartDate).toISOString().substring(0, 10)
              : '',
            actualEndDate: p.actualEndDate
              ? new Date(p.actualEndDate).toISOString().substring(0, 10)
              : '',
          });

          // Disable date fields if baselined
          if (this.isBaselined) {
            this.form.get('plannedStartDate')!.disable();
            this.form.get('plannedEndDate')!.disable();
          }

          this.loadWorkspaceOrgUnit(p.workspaceId);
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project.';
          this.isLoading = false;
        },
      });
  }

  /** Loads strategic objectives filtered by workspace org unit. */
  private loadStrategicObjectives(): void {
    if (!this.workspaceOrgUnit) return;

    this.strategicObjService
      .getByOrgUnit(this.workspaceOrgUnit)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (objectives) => {
          const data = (objectives as any)?.data ?? objectives;
          this.strategicObjectives = Array.isArray(data) ? data : [];
        },
        error: () => {
          this.strategicObjectives = [];
        },
      });
  }

  /** Filters the already-loaded users list by the workspace org unit. */
  private filterUsersByOrgUnit(): void {
    if (this.workspaceOrgUnit) {
      this.availableUsers = this.availableUsers.filter(
        u => u.department === this.workspaceOrgUnit
      );
    }
  }
}
