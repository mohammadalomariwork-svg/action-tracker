import { Component, OnInit, DestroyRef, inject, signal, effect, untracked } from '@angular/core';
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

import { forkJoin } from 'rxjs';

import { ProjectService } from '../../services/project.service';
import { MilestoneService } from '../../services/milestone.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { WorkspaceList, WorkspaceProjectManager } from '../../../workspaces/models/workspace.model';
import {
  ProjectType,
  ProjectStatus,
  ProjectPriority,
  ProjectResponse,
  StrategicObjectiveOption,
} from '../../models/project.models';
import { ActionItem, ActionItemFilter, ActionStatus } from '../../../../core/models/action-item.model';
import { ActionItemService } from '../../../../core/services/action-item.service';
import { PagedResult } from '../../../../core/models/api-response.model';
import { MilestoneResponse } from '../../models/milestone.models';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { WorkingDayHintComponent } from '../../../../shared/components/working-day-hint/working-day-hint.component';
import { WorkingDaysComponent } from '../../../../shared/components/working-days/working-days.component';
import { nonWorkingDatesMessage, workingDayValidator } from '../../../../shared/validators/working-day.validator';
import { injectWorkspaceCalendar } from '../../../../services/workspace-calendar.service';
import {
  CurrentProjectClassification, ProjectClassificationSelectComponent,
} from '../../../../shared/components/project-classification-select/project-classification-select.component';
import { internalClassificationPatch } from '../../../../shared/utils/project-classification-rules.util';
import { showErrorHere } from '../../../../core/http/server-reason';
import { editableStatusOptions, ProjectStatusOption, statusEditHint } from '../../../../shared/utils/project-edit-options.util';
import { ProjectPeopleFieldsComponent } from '../../../../shared/components/project-people-fields/project-people-fields.component';
import { ProjectPeopleService } from '../../../../services/project-people.service';
import { PersonOption, ProjectPeopleValue } from '../../../../models/project-people.model';
import { emptyPeopleValue, peoplePatch, peopleValueFrom, userListOptions } from '../../../../shared/utils/project-people.util';

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('plannedStartDate')?.value;
  const end = group.get('plannedEndDate')?.value;
  if (start && end && end <= start) return { dateRange: true };
  return null;
}

@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, RouterLink, BreadcrumbComponent, PageHeaderComponent,
    WorkingDayHintComponent, WorkingDaysComponent, ProjectClassificationSelectComponent,
    ProjectPeopleFieldsComponent,
  ],
  templateUrl: './project-form.component.html',
  styleUrl: './project-form.component.scss',
})
export class ProjectFormComponent implements OnInit {
  private readonly fb             = inject(FormBuilder);
  private readonly projectSvc     = inject(ProjectService);
  private readonly workspaceSvc   = inject(WorkspaceService);
  private readonly milestoneSvc   = inject(MilestoneService);
  private readonly actionItemSvc  = inject(ActionItemService);
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly destroyRef     = inject(DestroyRef);
  private readonly peopleSvc      = inject(ProjectPeopleService);

  isEditMode = false;
  projectId: string | null = null;
  workspaceId!: string;
  isBaselined = false;
  needsWorkspaceSelector = false;
  workspaces: WorkspaceList[] = [];

  strategicObjectives: StrategicObjectiveOption[] = [];

  /** PM dropdown is restricted to the workspace's Project Managers list. */
  workspacePms: WorkspaceProjectManager[] = [];
  workspacePmsLoaded = false;

  // ── People (WO-2026-11 P-2) ──
  people: ProjectPeopleValue = emptyPeopleValue();
  /** What the edited project had; null on create or when it could not be loaded. */
  private originalPeople: ProjectPeopleValue | null = null;
  peopleLoading = false;
  canAddProjectManagers = true;
  triedSave = false;
  /**
   * False for a caller who reaches the edited project only through a project
   * role: the workspace's lists (PMs, objectives, classifications, calendar)
   * answer 404 to them, so they are not asked for and those fields keep their value.
   */
  canOpenWorkspace = true;
  /** Lead PM's name for the read-only field. */
  leadPmName = '';
  /** The edited project cannot be changed by the caller (a viewer, sponsor or team member — P-2). */
  cannotEdit = false;
  /** WO-2026-11 P-2b (fix 1): the statuses a plain edit may set (the response's `allowedStatuses`). */
  statusOptions: ProjectStatusOption[] = [];
  statusHint: string | null = null;
  private currentObjective: StrategicObjectiveOption | null = null;

  isLoading = false;
  errorMessage: string | null = null;
  originalStatus: ProjectStatus | null = null;

  /** Internal classification the edited project holds now (WO-2026-11 P-1). */
  currentClassification: CurrentProjectClassification | null = null;
  private originalClassificationId: string | null = null;

  form!: FormGroup;

  // ── Working calendar of the project's workspace (WO-2026-11 C-3) ──
  private readonly calendarWorkspaceId = signal<string | null>(null);
  private readonly calendarViaProject = signal<string | null>(null);
  readonly calendar = injectWorkspaceCalendar(() => this.calendarWorkspaceId(), () => this.calendarViaProject());
  /** Planned dates the edited project holds now; unchanged dates are never flagged (Q8). */
  originalDates: { start: string | null; end: string | null } = { start: null, end: null };
  /** Re-runs the date validators when the calendar finishes loading. */
  private readonly revalidateDates = effect(() => {
    this.calendar();
    untracked(() => {
      this.form?.get('plannedStartDate')?.updateValueAndValidity({ emitEvent: false });
      this.form?.get('plannedEndDate')?.updateValueAndValidity({ emitEvent: false });
    });
  });

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
    if (wsId) {
      this.workspaceId = wsId;
      this.calendarWorkspaceId.set(wsId);
    } else if (!this.isEditMode) {
      this.needsWorkspaceSelector = true;
    }

    this.buildForm();

    if (this.workspaceId && !this.isEditMode) {
      this.loadWorkspacePms(this.workspaceId);
    }

    if (this.needsWorkspaceSelector) {
      this.loadWorkspaces();
    }

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

  /** The PM list as options for the additional-PM picker (D4: null for project-only callers). */
  get pmOptions(): PersonOption[] | null {
    return this.canOpenWorkspace ? userListOptions(this.workspacePms) : null;
  }

  onSubmit(): void {
    this.triedSave = true;
    const v = this.form.getRawValue();

    // Working-day rule on the planned dates (the validators block the save too;
    // this says why). The server applies the same check.
    const dateProblem = nonWorkingDatesMessage(this.calendar(), [
      { field: 'Planned start date', value: v.plannedStartDate, original: this.originalDates.start },
      { field: 'Planned end date',   value: v.plannedEndDate,   original: this.originalDates.end },
    ]);
    if (dateProblem) {
      this.form.markAllAsTouched();
      this.errorMessage = dateProblem;
      return;
    }

    if (this.form.invalid || this.people.sponsors.length === 0 || this.peopleLoading) {
      this.form.markAllAsTouched();
      if (this.people.sponsors.length === 0)
        this.errorMessage = 'At least one sponsor is required.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    if (this.isEditMode && this.projectId) {
      // Validate milestones/action items when transitioning to Active or Completed
      const needsValidation =
        (v.status === ProjectStatus.Active || v.status === ProjectStatus.Completed)
        && v.status !== this.originalStatus;

      if (needsValidation) {
        this.validateBeforeSave(v.status, () => this.doUpdate(v));
        return;
      }

      this.doUpdate(v);
    } else {
      const wsId = this.needsWorkspaceSelector ? v.selectedWorkspaceId : this.workspaceId;
      this.projectSvc.create({
        name: v.name,
        description: v.description || undefined,
        workspaceId: wsId,
        projectType: v.projectType,
        internalClassificationId: v.internalClassificationId || undefined,
        strategicObjectiveId: v.strategicObjectiveId || undefined,
        priority: v.priority,
        projectManagerUserId: v.projectManagerUserId,
        ...peoplePatch(null, this.people),
        plannedStartDate: v.plannedStartDate,
        plannedEndDate: v.plannedEndDate,
        approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.isLoading = false;
          const navTarget = wsId ? ['/workspaces', wsId] : ['/projects/my'];
          this.router.navigate(navTarget);
        },
        error: (err) => {
          this.errorMessage = showErrorHere(err) ?? 'Failed to create project.';
          this.isLoading = false;
        },
      });
    }
  }

  private doUpdate(v: any): void {
    this.projectSvc.update(this.projectId!, {
      name: v.name,
      description: v.description || undefined,
      projectType: v.projectType,
      ...internalClassificationPatch(this.originalClassificationId, v.internalClassificationId),
      status: v.status,
      strategicObjectiveId: v.strategicObjectiveId || undefined,
      priority: v.priority,
      projectManagerUserId: v.projectManagerUserId,
      ...peoplePatch(this.originalPeople, this.people),
      plannedStartDate: v.plannedStartDate,
      plannedEndDate: v.plannedEndDate,
      actualStartDate: v.actualStartDate || undefined,
      approvedBudget: v.approvedBudget ? +v.approvedBudget : undefined,
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(this.canOpenWorkspace ? ['/workspaces', this.workspaceId] : ['/projects', this.projectId]);
      },
      error: (err) => {
        this.errorMessage = showErrorHere(err) ?? err?.error?.detail ?? 'Failed to update project.';
        this.isLoading = false;
      },
    });
  }

  private validateBeforeSave(targetStatus: ProjectStatus, onValid: () => void): void {
    const label = targetStatus === ProjectStatus.Completed ? 'Completed' : 'Active';
    const actionFilter: ActionItemFilter = {
      projectId:      this.projectId!,
      pageNumber:     1,
      pageSize:       500,
      sortBy:         'dueDate',
      sortDescending: false,
    };

    forkJoin({
      milestones: this.milestoneSvc.getByProject(this.projectId!),
      actions:    this.actionItemSvc.getAll(actionFilter),
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ milestones, actions }) => {
        const msList     = milestones.data ?? [];
        const actionList = (actions.data as PagedResult<ActionItem>).items ?? [];

        if (msList.length === 0) {
          this.errorMessage = `Cannot set to ${label}: the project must have at least one milestone.`;
          this.isLoading = false;
          return;
        }

        const emptyMilestones = msList.filter((ms: MilestoneResponse) =>
          !actionList.some(a => a.milestoneId === ms.id)
        );

        if (emptyMilestones.length > 0) {
          const names = emptyMilestones.map((m: MilestoneResponse) => `"${m.name}"`).join(', ');
          this.errorMessage =
            `Cannot set to ${label}: the following milestone(s) have no action items — ${names}. ` +
            `Please add at least one action item to each milestone first.`;
          this.isLoading = false;
          return;
        }

        // When completing, all action items must be Done or Cancelled
        if (targetStatus === ProjectStatus.Completed) {
          const doneStatuses: (string | number)[] = [
            ActionStatus.Done, ActionStatus.Cancelled,
            'done', 'cancelled',
          ];
          const incompleteActions = actionList.filter(a =>
            !doneStatuses.includes(a.status as string | number)
          );

          if (incompleteActions.length > 0) {
            const names = incompleteActions.map(a => `"${a.title}"`).join(', ');
            this.errorMessage =
              `Cannot complete the project: all action items must be Done or Cancelled. ` +
              `Incomplete action items: ${names}.`;
            this.isLoading = false;
            return;
          }
        }

        onValid();
      },
      error: () => {
        this.errorMessage = 'Could not validate milestones. Please try again.';
        this.isLoading = false;
      },
    });
  }

  onCancel(): void {
    if (!this.canOpenWorkspace && this.projectId) this.router.navigate(['/projects', this.projectId]);
    else if (this.workspaceId) this.router.navigate(['/workspaces', this.workspaceId]);
    else if (this.needsWorkspaceSelector) this.router.navigate(['/projects/my']);
    else this.router.navigate(['/workspaces']);
  }

  private buildForm(): void {
    const formConfig: Record<string, any> = {
      name: ['', [Validators.required, Validators.maxLength(255)]],
      description: [''],
      projectType: [ProjectType.Operational, [Validators.required]],
      internalClassificationId: [null as string | null],
      strategicObjectiveId: [null as string | null],
      priority: [ProjectPriority.Medium, [Validators.required]],
      projectManagerUserId: ['', [Validators.required]],
      plannedStartDate: ['', [Validators.required,
        workingDayValidator(() => this.calendar(), () => this.originalDates.start, 'Planned start date')]],
      plannedEndDate: ['', [Validators.required,
        workingDayValidator(() => this.calendar(), () => this.originalDates.end, 'Planned end date')]],
      approvedBudget: [null as number | null],
      status: [ProjectStatus.Draft],
      actualStartDate: [''],
    };

    if (this.needsWorkspaceSelector) {
      formConfig['selectedWorkspaceId'] = ['', [Validators.required]];
    }

    this.form = this.fb.group(formConfig, { validators: dateRangeValidator });

    // When the user picks a workspace in the selector, re-load its PM list and
    // clear the PM selection (the old value almost certainly isn't on the new
    // workspace's list).
    if (this.needsWorkspaceSelector) {
      this.form.get('selectedWorkspaceId')!.valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((wsId: string) => {
          this.calendarWorkspaceId.set(wsId || null);
          // Classifications belong to one workspace (WO-2026-11 P-1).
          this.form.patchValue({ projectManagerUserId: '', internalClassificationId: null });
          // WO-2026-11 P-2: sponsors, viewers and team members are any active
          // user and stay; additional PMs come from the workspace's PM list.
          this.people = { ...this.people, additionalManagers: [] };
          this.loadWorkspacePms(wsId);
          if (this.isStrategic) {
            this.form.patchValue({ strategicObjectiveId: null });
            this.loadStrategicObjectives();
          }
        });
    }
  }

  private loadWorkspaces(): void {
    this.workspaceSvc.getWorkspaces()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => { this.workspaces = (res.data ?? []).filter(w => w.isActive); },
        error: () => { this.errorMessage = 'Failed to load workspaces.'; },
      });
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

  /** Loads the workspace's Project Managers — the PM dropdown is bound to this. */
  loadWorkspacePms(workspaceId: string): void {
    if (!workspaceId) {
      this.workspacePms = [];
      this.workspacePmsLoaded = false;
      return;
    }
    this.workspacePmsLoaded = false;
    this.workspaceSvc.getProjectManagers(workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspacePms = res.data ?? [];
          this.workspacePmsLoaded = true;
        },
        error: () => {
          this.workspacePms = [];
          this.workspacePmsLoaded = true;
        },
      });
  }

  private loadStrategicObjectives(): void {
    // Project-only callers keep the project's objective (the plan is the workspace's).
    if (!this.canOpenWorkspace) {
      this.strategicObjectives = this.currentObjective ? [this.currentObjective] : [];
      return;
    }
    const wsId = this.needsWorkspaceSelector
      ? this.form?.get('selectedWorkspaceId')?.value
      : this.workspaceId;
    if (!wsId) return;
    this.projectSvc.getStrategicObjectivesForWorkspace(wsId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.strategicObjectives = (res.data ?? []) as StrategicObjectiveOption[],
        error: () => this.strategicObjectives = [],
      });
  }

  /** Everyone on the edited project (GET /projects/:id/people) for the people block. */
  private loadPeople(projectId: string, sponsors: PersonOption[]): void {
    this.people = emptyPeopleValue(sponsors);
    this.originalPeople = null;
    this.canAddProjectManagers = false;
    this.peopleLoading = true;
    this.peopleSvc.get(projectId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        if (res.data) {
          this.people = peopleValueFrom(res.data);
          this.originalPeople = peopleValueFrom(res.data);
          this.canAddProjectManagers = res.data.canAddProjectManagers;
        }
        this.peopleLoading = false;
      },
      error: err => {
        this.errorMessage = showErrorHere(err) ?? "Could not load the project's people; they are left as they are.";
        this.peopleLoading = false;
      },
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
          this.calendarWorkspaceId.set(p.workspaceId);
          this.isBaselined = p.isBaselined;
          this.originalStatus = p.status;
          this.statusOptions = editableStatusOptions(p.status, p.allowedStatuses);
          this.statusHint = statusEditHint(p.status);
          this.canOpenWorkspace = p.canOpenWorkspace !== false;
          this.calendarViaProject.set(this.canOpenWorkspace ? null : p.id);
          this.leadPmName = p.projectManagerName;
          this.cannotEdit = p.canManage === false;
          if (this.cannotEdit)
            this.errorMessage = 'You can see this project, but only its project managers, the workspace’s admins, Admins and the PMO Head can edit it.';
          this.currentObjective = p.strategicObjectiveId
            ? { id: p.strategicObjectiveId, statement: p.strategicObjectiveStatement ?? 'Current objective', objectiveCode: '' }
            : null;
          this.loadPeople(p.id, p.sponsors.map(s => ({ userId: s.userId, fullName: s.fullName, email: s.email })));
          this.originalClassificationId = p.internalClassificationId ?? null;
          this.currentClassification = p.internalClassificationId
            ? { id: p.internalClassificationId, name: p.internalClassificationName, isActive: p.internalClassificationIsActive }
            : null;
          this.originalDates = {
            start: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : null,
            end:   p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : null,
          };
          if (this.canOpenWorkspace) this.loadWorkspacePms(this.workspaceId);

          this.form.patchValue({
            name: p.name,
            description: p.description ?? '',
            projectType: p.projectType,
            internalClassificationId: p.internalClassificationId ?? null,
            strategicObjectiveId: p.strategicObjectiveId ?? null,
            priority: p.priority,
            projectManagerUserId: p.projectManagerUserId,
            plannedStartDate: p.plannedStartDate ? String(p.plannedStartDate).substring(0, 10) : '',
            plannedEndDate: p.plannedEndDate ? String(p.plannedEndDate).substring(0, 10) : '',
            approvedBudget: p.approvedBudget ?? null,
            status: p.status,
            actualStartDate: p.actualStartDate ? String(p.actualStartDate).substring(0, 10) : '',
          });

          // Freeze dates when project is not Draft (submitted for approval or active)
          const isNotDraft = p.status !== ProjectStatus.Draft;
          if (this.isBaselined || isNotDraft) {
            this.form.get('plannedStartDate')!.disable();
            this.form.get('plannedEndDate')!.disable();
          }

          // Project-only callers cannot change the lead PM (D4).
          if (!this.canOpenWorkspace) this.form.get('projectManagerUserId')!.disable();

          // Disable status when PendingApproval (workflow-controlled)
          if (p.status === ProjectStatus.PendingApproval) {
            this.form.get('status')?.disable();
          }

          if (p.projectType === ProjectType.Strategic) {
            this.loadStrategicObjectives();
          }

          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = showErrorHere(err) ?? 'Failed to load project.';
          this.isLoading = false;
        },
      });
  }
}
