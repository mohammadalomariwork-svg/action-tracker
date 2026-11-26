import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { StrategicPlanService } from '../../admin/services/strategic-plan.service';
import { StrategicObjectiveService } from '../../admin/services/strategic-objective.service';
import { KpiService } from '../../admin/services/kpi.service';
import { Kpi, MeasurementPeriodLabels, type MeasurementPeriod } from '../../admin/models/kpi.models';
import { OrgUnitService } from '../../admin/services/org-unit.service';
import { WorkspaceService } from '../../workspaces/services/workspace.service';
import { WorkspaceSummary } from '../../workspaces/models/workspace.model';
import { ProjectService } from '../../projects/services/project.service';
import { ProjectResponse, ProjectStatus, ProjectType } from '../../projects/models/project.models';
import { StrategicPlanWorkflowService } from '../../../services/strategic-plan-workflow.service';
import { WorkflowStateService } from '../../../services/workflow-state.service';
import { ToastService } from '../../../core/services/toast.service';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  CreateStrategicPlanRequest,
  StrategicPlan,
  StrategicPlanStatus,
  UpdateStrategicPlanRequest,
} from '../../admin/models/strategic-plans.models';
import { StrategicObjective } from '../../admin/models/strategic-objectives.models';
import { OrgUnit } from '../../admin/models/org-chart.models';
import {
  StrategicPlanApprovalRequest,
  StrategicPlanApprover,
  StrategicPlanLockState,
} from '../../../models/strategic-plan-workflow.model';
import { PlanFormDialogComponent } from '../components/plan-form-dialog/plan-form-dialog.component';
import { JustificationDialogComponent } from '../components/justification-dialog/justification-dialog.component';
import { ManageApproversDialogComponent } from '../components/manage-approvers-dialog/manage-approvers-dialog.component';
import { ObjectiveFormComponent } from '../../admin/components/strategic-objectives/objective-form.component';
import { PermissionStateService } from '../../permissions/services/permission-state.service';
import { OrgUnitOption } from '../../../shared';
import { showErrorHere } from '../../../core/http/server-reason';

@Component({
  selector: 'app-strategy-page',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, HasPermissionDirective,
    BreadcrumbComponent, PageHeaderComponent,
    PlanFormDialogComponent, JustificationDialogComponent,
    ManageApproversDialogComponent,
    ObjectiveFormComponent,
  ],
  templateUrl: './strategy-page.component.html',
  styleUrl: './strategy-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly planService = inject(StrategicPlanService);
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly kpiService = inject(KpiService);
  private readonly orgUnitService = inject(OrgUnitService);
  private readonly workspaceService = inject(WorkspaceService);
  private readonly projectService = inject(ProjectService);
  private readonly workflow = inject(StrategicPlanWorkflowService);
  private readonly workflowState = inject(WorkflowStateService);
  private readonly permissionState = inject(PermissionStateService);
  private readonly toast = inject(ToastService);

  readonly orgUnitId = signal<string>('');
  readonly orgUnit = signal<OrgUnit | null>(null);
  readonly activePlan = signal<StrategicPlan | null>(null);
  readonly objectives = signal<StrategicObjective[]>([]);
  readonly summary = signal<WorkspaceSummary | null>(null);
  readonly projects = signal<ProjectResponse[]>([]);
  readonly projectsLoading = signal(false);
  readonly projectsError = signal<string | null>(null);
  readonly kpis = signal<Kpi[]>([]);
  readonly kpisLoading = signal(false);
  readonly kpisError = signal<string | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly ProjectStatus = ProjectStatus;
  readonly MeasurementPeriodLabels = MeasurementPeriodLabels;

  /** Page size shared by the three in-page lists (objectives / KPIs / projects). */
  readonly PAGE_SIZE = 5;

  // Per-list current page (1-based). Reset to 1 each time the underlying data
  // reloads — see the corresponding load methods + reload().
  readonly objectivesPage = signal(1);
  readonly kpisPage = signal(1);
  readonly projectsPage = signal(1);

  readonly objectivesPageCount = computed(() =>
    Math.max(1, Math.ceil(this.objectives().length / this.PAGE_SIZE)),
  );
  readonly kpisPageCount = computed(() =>
    Math.max(1, Math.ceil(this.kpis().length / this.PAGE_SIZE)),
  );
  readonly projectsPageCount = computed(() =>
    Math.max(1, Math.ceil(this.projects().length / this.PAGE_SIZE)),
  );

  readonly pagedObjectives = computed(() => {
    const start = (this.objectivesPage() - 1) * this.PAGE_SIZE;
    return this.objectives().slice(start, start + this.PAGE_SIZE);
  });
  readonly pagedKpis = computed(() => {
    const start = (this.kpisPage() - 1) * this.PAGE_SIZE;
    return this.kpis().slice(start, start + this.PAGE_SIZE);
  });
  readonly pagedProjects = computed(() => {
    const start = (this.projectsPage() - 1) * this.PAGE_SIZE;
    return this.projects().slice(start, start + this.PAGE_SIZE);
  });

  /** Total KPIs across non-deleted objectives in this org unit. */
  readonly totalKpis = computed(() =>
    this.objectives().reduce((sum, o) => sum + (o.kpiCount ?? 0), 0),
  );

  /** Strategic projects linked to one of this plan's objectives. */
  readonly totalProjects = computed(() => this.projects().length);

  readonly dialogOpen = signal(false);
  readonly dialogMode = signal<'create' | 'edit'>('create');
  readonly dialogError = signal<string | null>(null);
  /**
   * Options for the dialog's org-unit picker. Populated lazily by `openEdit()` —
   * we only resolve the user's visible units (and which of them already have an
   * in-flight plan) when the user actually decides to edit, instead of paying
   * that cost on every page load. Empty in create mode (handled by shell page).
   */
  readonly dialogOrgUnitOptions = signal<ReadonlyArray<OrgUnitOption>>([]);

  // ── Workflow state ─────────────────────────────────────
  readonly approvalHistory = signal<StrategicPlanApprovalRequest[]>([]);
  readonly lockState = signal<StrategicPlanLockState | null>(null);
  readonly approverPreview = signal<{ displayName: string; sourceOrgUnitName: string } | null>(null);
  readonly approvers = signal<StrategicPlanApprover[]>([]);
  readonly approversDialogOpen = signal(false);

  /** Approvers can only be configured while the plan is in Draft. */
  readonly canManageApprovers = computed(() => {
    const plan = this.activePlan();
    return !!plan && plan.status === StrategicPlanStatus.Draft;
  });

  /** Currently-pending approval request (any level), if any. Drives the
   *  highlight on the chain progress display. */
  readonly currentPendingRequest = computed(() => {
    const list = this.approvalHistory();
    // History is ordered newest-first, so the first Pending row is the active one.
    return list.find(r => (r.status === 'Pending' || r.status === 0)) ?? null;
  });

  // Submit / revision dialog
  readonly justificationOpen = signal(false);
  readonly justificationMode = signal<'submit' | 'revision'>('submit');
  readonly justificationError = signal<string | null>(null);
  readonly justificationSubmitting = signal(false);

  readonly StrategicPlanStatus = StrategicPlanStatus;

  readonly canSubmit = computed(() => {
    const plan = this.activePlan();
    return !!plan && plan.status === StrategicPlanStatus.Draft;
  });

  // Objective offcanvas — handles both Add and Edit. Only meaningful when the
  // plan is in Draft (Approved/PendingApproval/Archived block mutations
  // upstream). `editingObjective` null = Add, non-null = Edit that row.
  readonly objectiveOffcanvasOpen = signal(false);
  readonly editingObjective = signal<StrategicObjective | null>(null);

  readonly canAddObjective = computed(() => {
    const plan = this.activePlan();
    return !!plan && plan.status === StrategicPlanStatus.Draft;
  });

  /** Same gate as add — Draft plans are the only state where objectives can mutate. */
  readonly canEditObjective = this.canAddObjective;

  readonly canRequestRevision = computed(() => {
    const plan = this.activePlan();
    const lock = this.lockState();
    return !!plan
      && plan.status === StrategicPlanStatus.Approved
      && (!lock || lock.pendingRevisionId == null);
  });

  readonly hasPendingRevision = computed(() => {
    const lock = this.lockState();
    return !!lock?.pendingRevisionId;
  });

  readonly isPlanLocked = computed(() => this.lockState()?.isLocked ?? false);

  readonly statusBadgeClass = computed(() => {
    const plan = this.activePlan();
    if (!plan) return 'archived';
    switch (plan.status) {
      case StrategicPlanStatus.Draft: return 'draft';
      case StrategicPlanStatus.PendingApproval: return 'pending';
      case StrategicPlanStatus.Approved: return 'approved';
      case StrategicPlanStatus.Archived: return 'archived';
      default: return 'archived';
    }
  });

  readonly statusLabel = computed(() => {
    const plan = this.activePlan();
    if (!plan) return '';
    switch (plan.status) {
      case StrategicPlanStatus.Draft: return 'Draft';
      case StrategicPlanStatus.PendingApproval: return 'Pending approval';
      case StrategicPlanStatus.Approved: return 'Approved';
      case StrategicPlanStatus.Archived: return 'Archived';
      default: return 'Unknown';
    }
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('orgUnitId') ?? '';
      this.orgUnitId.set(id);
      this.reload();
    });
  }

  /** Scrolls the "Strategic objectives" section into view. Wired to the
   *  Objectives stat card. */
  scrollToObjectives(): void {
    const el = document.getElementById('sp-objectives-section');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** Scrolls the in-page "KPIs" section into view. */
  scrollToKpis(): void {
    const el = document.getElementById('sp-kpis-section');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** Scrolls the in-page "Strategic projects" section into view. The list is
   *  populated by `loadProjects()` from the plan's objectives. */
  scrollToProjects(): void {
    const el = document.getElementById('sp-projects-section');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ── Pagination handlers ─────────────────────────────────────────────
  setObjectivesPage(p: number): void {
    this.objectivesPage.set(this.clampPage(p, this.objectivesPageCount()));
  }
  setKpisPage(p: number): void {
    this.kpisPage.set(this.clampPage(p, this.kpisPageCount()));
  }
  setProjectsPage(p: number): void {
    this.projectsPage.set(this.clampPage(p, this.projectsPageCount()));
  }
  private clampPage(p: number, total: number): number {
    if (p < 1) return 1;
    if (p > total) return total;
    return p;
  }
  private resetPages(): void {
    this.objectivesPage.set(1);
    this.kpisPage.set(1);
    this.projectsPage.set(1);
  }

  private reload(): void {
    const id = this.orgUnitId();
    if (!id) return;
    this.loading.set(true);
    this.error.set(null);

    // Phase 1: load org unit + plan list + summary. We need the active plan id
    // before we can scope objectives / projects / KPIs to that specific plan.
    forkJoin({
      orgUnit: this.orgUnitService.getById(id),
      plans: this.planService.getByOrgUnit(id),
      summary: this.workspaceService.getSummary(id),
    }).subscribe({
      next: ({ orgUnit, plans, summary }) => {
        this.orgUnit.set(orgUnit);
        // Pick the latest in-flight plan (Draft, PendingApproval, or Approved).
        // Plans are returned newest-first from the API.
        const active = plans.find(p => p.status !== StrategicPlanStatus.Archived) ?? null;
        this.activePlan.set(active);
        this.summary.set(summary?.data ?? null);
        this.loadWorkflowState();

        if (!active) {
          this.objectives.set([]);
          this.projects.set([]);
          this.kpis.set([]);
          this.resetPages();
          this.loading.set(false);
          return;
        }

        // Phase 2: scope objectives strictly to *this* plan via the dedicated
        // by-plan endpoint. Then fan out to projects + KPIs from those ids.
        this.objectiveService.getByPlan(active.id).subscribe({
          next: objs => {
            this.objectives.set(objs.filter(o => !o.isDeleted));
            this.resetPages();
            this.loadProjects();
            this.loadKpis();
            this.loading.set(false);
          },
          error: err => {
            this.objectives.set([]);
            this.projects.set([]);
            this.kpis.set([]);
            this.resetPages();
            this.error.set(showErrorHere(err) ?? 'Failed to load strategic objectives.');
            this.loading.set(false);
          },
        });
      },
      error: err => {
        this.error.set(showErrorHere(err) ?? 'Failed to load strategy data.');
        this.loading.set(false);
      },
    });
  }

  /** Loads strategic projects whose StrategicObjectiveId matches one of the
   *  current plan's objectives. Skipped (and the list cleared) when there are
   *  no objectives, since no project could be linked. */
  private loadProjects(): void {
    const objectiveIds = this.objectives().map(o => o.id);
    if (objectiveIds.length === 0) {
      this.projects.set([]);
      this.projectsError.set(null);
      return;
    }

    this.projectsLoading.set(true);
    this.projectsError.set(null);

    this.projectService.getAll({
      pageNumber: 1,
      pageSize: 100,
      sortBy: 'createdAt',
      sortDescending: true,
      strategicObjectiveIds: objectiveIds,
      projectType: ProjectType.Strategic,
    }).subscribe({
      next: res => {
        const items = res?.data?.items ?? [];
        this.projects.set(items.filter(p => !p.isDeleted));
        this.projectsLoading.set(false);
      },
      error: err => {
        this.projects.set([]);
        this.projectsError.set(showErrorHere(err) ?? 'Failed to load projects.');
        this.projectsLoading.set(false);
      },
    });
  }

  /** Loads all KPIs that belong to the current plan's objectives. Fans out one
   *  `getByObjective` request per objective; with typical plan sizes (≤ ~15
   *  objectives) this is fine. Skipped + cleared when there are no objectives. */
  private loadKpis(): void {
    const objs = this.objectives();
    if (objs.length === 0) {
      this.kpis.set([]);
      this.kpisError.set(null);
      return;
    }

    this.kpisLoading.set(true);
    this.kpisError.set(null);

    forkJoin(objs.map(o => this.kpiService.getByObjective(o.id))).subscribe({
      next: groups => {
        const flat = groups.flat().filter(k => !k.isDeleted);
        // Sort by objective code, then KPI number, so KPIs read in the same
        // order users see in the objectives list above.
        flat.sort((a, b) => {
          const c = (a.objectiveCode || '').localeCompare(b.objectiveCode || '');
          return c !== 0 ? c : (a.kpiNumber - b.kpiNumber);
        });
        this.kpis.set(flat);
        this.kpisLoading.set(false);
      },
      error: err => {
        this.kpis.set([]);
        this.kpisError.set(showErrorHere(err) ?? 'Failed to load KPIs.');
        this.kpisLoading.set(false);
      },
    });
  }

  private loadWorkflowState(): void {
    const plan = this.activePlan();
    const orgUnitId = this.orgUnitId();
    if (!plan || !orgUnitId) {
      this.approvalHistory.set([]);
      this.lockState.set(null);
      this.approverPreview.set(null);
      this.approvers.set([]);
      return;
    }

    this.workflow.getForPlan(plan.id).subscribe({
      next: r => { if (r.success) this.approvalHistory.set(r.data); },
      error: () => {},
    });
    this.workflow.getLockState(plan.id).subscribe({
      next: r => { if (r.success) this.lockState.set(r.data); },
      error: () => {},
    });
    this.orgUnitService.getPlanApprover(orgUnitId).subscribe({
      next: r => { if (r.success && r.data) this.approverPreview.set(r.data); },
      error: () => this.approverPreview.set(null),
    });
    // Configured approval chain (multi-level). Empty = legacy single-level
    // fallback (level-3 head); the UI surfaces that distinction via the
    // approver-preview line above.
    this.workflow.getApprovers(plan.id).subscribe({
      next: r => { if (r.success) this.approvers.set(r.data ?? []); },
      error: () => this.approvers.set([]),
    });
  }

  // ── Manage approvers (Draft only) ──────────────────────
  openApproversDialog(): void {
    this.approversDialogOpen.set(true);
  }

  closeApproversDialog(): void {
    this.approversDialogOpen.set(false);
  }

  onApproversSaved(rows: StrategicPlanApprover[]): void {
    this.approvers.set(rows);
    this.approversDialogOpen.set(false);
    this.toast.success('Approval chain updated.');
  }

  openCreate(): void {
    this.dialogMode.set('create');
    this.dialogError.set(null);
    this.dialogOpen.set(true);
  }

  openEdit(): void {
    this.dialogMode.set('edit');
    this.dialogError.set(null);

    // Resolve the org-unit options before opening so the picker isn't briefly
    // empty. Includes the plan's current unit + every other in-scope unit that
    // doesn't already host a non-archived plan, so the user can re-assign a
    // Draft plan without colliding with another in-flight plan. Tolerates per-
    // unit fetch failures (a single 403 shouldn't block the whole dialog).
    const visibleIds = this.permissionState.getVisibleOrgUnitIds();
    const currentPlanUnitId = this.activePlan()?.orgUnitId ?? this.orgUnitId();

    this.orgUnitService.getAll(1, 500).subscribe({
      next: res => {
        const units = (res.orgUnits ?? []).filter(u => !u.isDeleted);
        const inScope = visibleIds.length === 0
          ? units
          : units.filter(u => visibleIds.includes(u.id));

        if (inScope.length === 0) {
          this.dialogOrgUnitOptions.set([]);
          this.dialogOpen.set(true);
          return;
        }

        const planRequests = inScope.map(u =>
          this.planService.getByOrgUnit(u.id).pipe(
            catchError(() => of([] as StrategicPlan[])),
            map(plans => ({ unit: u, plans })),
          ),
        );

        forkJoin(planRequests).subscribe({
          next: results => {
            const taken = new Set(
              results
                .filter(r => r.plans.some(
                  p => p.status !== StrategicPlanStatus.Archived
                    && p.orgUnitId !== currentPlanUnitId,
                ))
                .map(r => r.unit.id),
            );
            const options: OrgUnitOption[] = inScope
              .filter(u => u.id === currentPlanUnitId || !taken.has(u.id))
              .sort((a, b) => a.level - b.level || a.name.localeCompare(b.name))
              .map(u => ({ id: u.id, name: u.name, level: u.level, code: u.code }));
            this.dialogOrgUnitOptions.set(options);
            this.dialogOpen.set(true);
          },
          error: () => {
            this.dialogOrgUnitOptions.set([]);
            this.dialogOpen.set(true);
          },
        });
      },
      error: () => {
        this.dialogOrgUnitOptions.set([]);
        this.dialogOpen.set(true);
      },
    });
  }

  closeDialog(): void {
    this.dialogOpen.set(false);
  }

  onSavePlan(payload: { name: string; startYear: number; durationYears: number; orgUnitId?: string }): void {
    if (this.dialogMode() === 'create') {
      const request: CreateStrategicPlanRequest = {
        name: payload.name,
        orgUnitId: this.orgUnitId(),
        startYear: payload.startYear,
        durationYears: payload.durationYears,
      };
      this.planService.create(request).subscribe({
        next: plan => {
          this.activePlan.set(plan);
          this.toast.success('Strategic plan created.');
          this.dialogOpen.set(false);
        },
        error: err => {
          const msg = showErrorHere(err) ?? 'Failed to create plan.';
          this.dialogError.set(msg);
        },
      });
    } else {
      const plan = this.activePlan();
      if (!plan) return;

      // Only forward orgUnitId on edit when it actually changed — sending the
      // same id is a no-op upstream, but skipping it keeps the UpdateAsync
      // path's "did the unit move?" branch off when nothing changed.
      const reassignedTo = payload.orgUnitId && payload.orgUnitId !== plan.orgUnitId
        ? payload.orgUnitId
        : undefined;

      const request: UpdateStrategicPlanRequest = {
        name: payload.name,
        startYear: payload.startYear,
        durationYears: payload.durationYears,
        ...(reassignedTo ? { orgUnitId: reassignedTo } : {}),
      };
      this.planService.update(plan.id, request).subscribe({
        next: updated => {
          this.activePlan.set(updated);
          this.toast.success('Strategic plan updated.');
          this.dialogOpen.set(false);

          // If the plan moved to a different org unit, the URL is stale — the
          // page is keyed on `:orgUnitId`, so navigate to the new unit's
          // strategy page so the user lands where their plan lives now.
          if (reassignedTo && reassignedTo !== this.orgUnitId()) {
            this.router.navigate(['/strategy/org-units', reassignedTo]);
          }
        },
        error: err => {
          this.dialogError.set(showErrorHere(err) ?? 'Failed to update plan.');
        },
      });
    }
  }

  // ── Add / Edit Strategic Objective (Draft plans only) ─────────
  openAddObjective(): void {
    this.editingObjective.set(null);
    this.objectiveOffcanvasOpen.set(true);
  }

  openEditObjective(obj: StrategicObjective): void {
    this.editingObjective.set(obj);
    this.objectiveOffcanvasOpen.set(true);
  }

  closeObjectiveOffcanvas(): void {
    this.objectiveOffcanvasOpen.set(false);
    this.editingObjective.set(null);
  }

  onObjectiveSaved(_obj: StrategicObjective): void {
    const wasEdit = !!this.editingObjective();
    this.objectiveOffcanvasOpen.set(false);
    this.editingObjective.set(null);
    this.toast.success(wasEdit ? 'Strategic objective updated.' : 'Strategic objective created.');

    // Refresh the plan-scoped objectives list so the new/edited entry appears
    // with its server-assigned code, then re-fan KPI/project loads since
    // downstream sections derive from objective ids.
    const plan = this.activePlan();
    if (!plan) return;
    this.objectiveService.getByPlan(plan.id).subscribe({
      next: objs => {
        this.objectives.set(objs.filter(o => !o.isDeleted));
        this.resetPages();
        this.loadKpis();
        this.loadProjects();
      },
    });
  }

  archivePlan(): void {
    const plan = this.activePlan();
    if (!plan) return;
    if (!confirm(`Archive plan "${plan.name}"? It will be soft-deleted.`)) return;
    this.planService.softDelete(plan.id).subscribe({
      next: () => {
        this.activePlan.set(null);
        this.toast.success('Strategic plan archived.');
      },
      error: err => {
        this.toast.error(err?.error?.message ?? 'Failed to archive plan.');
      },
    });
  }

  // ── CR-001-D / CR-001-E: Submit / Revision flow ────────

  openSubmitDialog(): void {
    // Pre-flight: every objective in the plan (including those in descendant
    // org units — the strategy-page loader already pulls them all via
    // `getByPlan`) must have at least one KPI. The backend enforces the same
    // rule on `POST /workflow/submit`; checking here lets us fail fast with a
    // toast before the user types a justification, listing exactly which
    // objectives need KPIs.
    const objs = this.objectives();
    if (objs.length === 0) {
      this.toast.error(
        'The strategy must have at least one strategic objective before submitting for approval.',
      );
      return;
    }
    const empty = objs.filter(o => (o.kpiCount ?? 0) === 0);
    if (empty.length > 0) {
      const names = empty.map(o => o.objectiveCode || o.statement).join(', ');
      this.toast.error(
        `Every strategic objective (including sub-unit ones) must have at least one KPI. Missing: ${names}.`,
      );
      return;
    }

    // Per-KPI target completeness — best-effort heuristic on the page's already-
    // loaded `kpis()` signal. Catches "no targets at all" / "obviously short"
    // cases (Quarterly KPI with 2 target rows, etc.). The backend runs the
    // precise per-month / per-year check on POST /workflow/submit, so an
    // off-year filling will still be caught there with a clearer message.
    // `MeasurementPeriod` is a numeric union (1=Monthly, 2=Quarterly,
    // 3=SemiAnnual, 4=Yearly) — keyed numerically because there's no enum.
    const requiredPerPeriod: Record<MeasurementPeriod, number> = {
      1: 12,  // Monthly
      2:  4,  // Quarterly
      3:  2,  // SemiAnnual
      4:  1,  // Yearly
    };
    const incompleteKpis = this.kpis().filter(k => {
      const required = requiredPerPeriod[k.periodValue] ?? 0;
      return (k.targetCount ?? 0) < required;
    });
    if (incompleteKpis.length > 0) {
      const labels = incompleteKpis
        .map(k => `${k.objectiveCode}-K${k.kpiNumber} (${MeasurementPeriodLabels[k.periodValue]} needs ${requiredPerPeriod[k.periodValue]})`)
        .join(', ');
      this.toast.error(
        `Every KPI must have its full set of targets entered (Yearly = 1, Semi-Annual = 2, Quarterly = 4, Monthly = 12). Incomplete: ${labels}.`,
      );
      return;
    }

    this.justificationMode.set('submit');
    this.justificationError.set(null);
    this.justificationOpen.set(true);
  }

  openRevisionDialog(): void {
    this.justificationMode.set('revision');
    this.justificationError.set(null);
    this.justificationOpen.set(true);
  }

  closeJustificationDialog(): void {
    this.justificationOpen.set(false);
    this.justificationSubmitting.set(false);
  }

  onJustificationSubmit(justification: string): void {
    const plan = this.activePlan();
    if (!plan) return;
    this.justificationSubmitting.set(true);
    this.justificationError.set(null);

    if (this.justificationMode() === 'submit') {
      this.workflow.submit({ planId: plan.id, justification }).subscribe({
        next: () => {
          this.toast.success('Plan submitted for approval.');
          this.justificationOpen.set(false);
          this.justificationSubmitting.set(false);
          this.workflowState.refreshPendingCount();
          this.reload();
        },
        error: err => {
          this.justificationSubmitting.set(false);
          this.justificationError.set(showErrorHere(err) ?? 'Failed to submit request.');
        },
      });
      return;
    }

    // Revision flow (CR-001-G): open a draft first, then submit it for
    // approval. We chain the two calls so the existing single-click UX
    // still works — PR 3 will expose the intermediate edit step.
    this.workflow.openDraft(plan.id).subscribe({
      next: () => {
        this.workflow.requestRevision({ planId: plan.id, justification }).subscribe({
          next: () => {
            this.toast.success('Revision request submitted.');
            this.justificationOpen.set(false);
            this.justificationSubmitting.set(false);
            this.workflowState.refreshPendingCount();
            this.reload();
          },
          error: err => {
            this.justificationSubmitting.set(false);
            this.justificationError.set(showErrorHere(err) ?? 'Failed to submit revision request.');
          },
        });
      },
      error: err => {
        this.justificationSubmitting.set(false);
        this.justificationError.set(showErrorHere(err) ?? 'Failed to open revision draft.');
      },
    });
  }
}
