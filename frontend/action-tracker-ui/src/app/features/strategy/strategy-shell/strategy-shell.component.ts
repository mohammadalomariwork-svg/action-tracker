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
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { OrgUnitService } from '../../admin/services/org-unit.service';
import { OrgUnit } from '../../admin/models/org-chart.models';
import { PermissionStateService } from '../../permissions/services/permission-state.service';
import { StrategicPlanService } from '../../admin/services/strategic-plan.service';
import { StrategicObjectiveService } from '../../admin/services/strategic-objective.service';
import { StrategicObjective } from '../../admin/models/strategic-objectives.models';
import { ToastService } from '../../../core/services/toast.service';
import {
  CreateStrategicPlanRequest,
  StrategicPlan,
  StrategicPlanStatus,
} from '../../admin/models/strategic-plans.models';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { BreadcrumbComponent } from '../../../shared/components/breadcrumb/breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { OrgUnitOption } from '../../../shared';
import { PlanFormDialogComponent } from '../components/plan-form-dialog/plan-form-dialog.component';
import { showErrorHere } from '../../../core/http/server-reason';

interface StrategyCard {
  plan: StrategicPlan;
  orgUnit: OrgUnit;
}

@Component({
  selector: 'app-strategy-shell',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    HasPermissionDirective,
    BreadcrumbComponent,
    PageHeaderComponent,
    PlanFormDialogComponent,
  ],
  templateUrl: './strategy-shell.component.html',
  styleUrl: './strategy-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StrategyShellComponent implements OnInit {
  private readonly orgUnitService = inject(OrgUnitService);
  private readonly permissionState = inject(PermissionStateService);
  private readonly planService = inject(StrategicPlanService);
  private readonly objectiveService = inject(StrategicObjectiveService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly StrategicPlanStatus = StrategicPlanStatus;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchText = signal('');

  /** All org units the user can access (after permission filtering). */
  private readonly visibleOrgUnits = signal<OrgUnit[]>([]);
  readonly visibleOrgUnitsCount = computed(() => this.visibleOrgUnits().length);
  /** All non-archived plans across the user's visible org units. */
  private readonly plans = signal<StrategicPlan[]>([]);
  /** All non-deleted objectives across the user's visible org units (for total counts). */
  private readonly objectives = signal<StrategicObjective[]>([]);

  /** Cards = one per existing in-flight plan in scope. */
  readonly cards = computed<StrategyCard[]>(() => {
    const unitsById = new Map(this.visibleOrgUnits().map(u => [u.id, u]));
    return this.plans()
      .filter(p => p.status !== StrategicPlanStatus.Archived)
      .map(p => ({ plan: p, orgUnit: unitsById.get(p.orgUnitId) ?? this.synthesizeOrgUnit(p) }));
  });

  readonly filteredCards = computed<StrategyCard[]>(() => {
    const text = this.searchText().trim().toLowerCase();
    const cards = this.cards();
    if (!text) return cards;
    return cards.filter(c =>
      c.plan.name.toLowerCase().includes(text) ||
      c.orgUnit.name.toLowerCase().includes(text) ||
      (c.orgUnit.code?.toLowerCase().includes(text) ?? false)
    );
  });

  // ── Stats ──────────────────────────────────────────────────────────────
  readonly totalCount = computed(() => this.cards().length);
  readonly approvedCount = computed(
    () => this.cards().filter(c => c.plan.status === StrategicPlanStatus.Approved).length,
  );
  readonly draftCount = computed(
    () => this.cards().filter(c => c.plan.status === StrategicPlanStatus.Draft).length,
  );
  readonly pendingCount = computed(
    () => this.cards().filter(c => c.plan.status === StrategicPlanStatus.PendingApproval).length,
  );
  /**
   * Total objectives across in-flight plans in scope. Sourced from each plan's
   * server-side `objectiveCount` so it stays consistent with the per-plan badge
   * shown on cards.
   */
  readonly totalObjectives = computed(() =>
    this.cards().reduce((sum, c) => sum + (c.plan.objectiveCount ?? 0), 0),
  );
  /**
   * Total KPIs across in-flight plans in scope. Sourced from `kpiCount` on each
   * objective whose org unit hosts an in-flight plan (so archived org-unit KPIs
   * are not counted).
   */
  readonly totalKpis = computed(() => {
    const inflightUnitIds = new Set(this.cards().map(c => c.orgUnit.id));
    return this.objectives()
      .filter(o => inflightUnitIds.has(o.orgUnitId))
      .reduce((sum, o) => sum + (o.kpiCount ?? 0), 0);
  });

  /** Org units in scope that have NO in-flight plan — eligible for "New Strategy". */
  readonly availableCreateUnits = computed<OrgUnitOption[]>(() => {
    const planByUnit = new Set(
      this.plans()
        .filter(p => p.status !== StrategicPlanStatus.Archived)
        .map(p => p.orgUnitId),
    );
    return this.visibleOrgUnits()
      .filter(u => !planByUnit.has(u.id))
      .map(u => ({ id: u.id, name: u.name, level: u.level, code: u.code }));
  });

  // ── Create dialog ──────────────────────────────────────────────────────
  readonly dialogOpen = signal(false);
  readonly dialogError = signal<string | null>(null);

  ngOnInit(): void {
    this.loadStrategies();
  }

  private loadStrategies(): void {
    this.loading.set(true);
    this.error.set(null);

    this.orgUnitService.getAll(1, 500).subscribe({
      next: res => {
        const units = (res.orgUnits ?? []).filter(u => !u.isDeleted);
        const visibleIds = this.permissionState.getVisibleOrgUnitIds();
        const filteredUnits = visibleIds.length === 0
          ? units
          : units.filter(u => visibleIds.includes(u.id));
        this.visibleOrgUnits.set(filteredUnits);

        if (filteredUnits.length === 0) {
          this.plans.set([]);
          this.objectives.set([]);
          this.loading.set(false);
          return;
        }

        // Fetch plans + objectives for each visible unit in parallel; tolerate
        // per-unit errors so a single 403/404 doesn't blank the whole page.
        const planRequests = filteredUnits.map(u =>
          this.planService.getByOrgUnit(u.id).pipe(
            catchError(() => of([] as StrategicPlan[])),
          ),
        );
        const objectiveRequests = filteredUnits.map(u =>
          this.objectiveService.getByOrgUnit(u.id).pipe(
            catchError(() => of([] as StrategicObjective[])),
          ),
        );

        forkJoin({
          plans: forkJoin(planRequests).pipe(map(arrs => arrs.flat())),
          objectives: forkJoin(objectiveRequests).pipe(map(arrs => arrs.flat())),
        }).subscribe({
          next: ({ plans, objectives }) => {
            this.plans.set(plans);
            this.objectives.set(objectives.filter(o => !o.isDeleted));
            this.loading.set(false);
          },
          error: err => {
            this.error.set(err?.error?.message ?? 'Failed to load strategic plans.');
            this.loading.set(false);
          },
        });
      },
      error: err => {
        this.error.set(showErrorHere(err) ?? 'Failed to load org units.');
        this.loading.set(false);
      },
    });
  }

  onCardClick(card: StrategyCard): void {
    this.router.navigate(['/strategy/org-units', card.orgUnit.id]);
  }

  onCreateNew(): void {
    if (this.availableCreateUnits().length === 0) {
      this.toast.info(
        'Every organisation unit in your scope already has an active strategic plan.',
      );
      return;
    }
    this.dialogError.set(null);
    this.dialogOpen.set(true);
  }

  closeDialog(): void {
    this.dialogOpen.set(false);
    this.dialogError.set(null);
  }

  onSaveNewPlan(payload: {
    name: string;
    startYear: number;
    durationYears: number;
    orgUnitId?: string;
  }): void {
    if (!payload.orgUnitId) {
      this.dialogError.set('Please pick an organisation unit.');
      return;
    }
    const request: CreateStrategicPlanRequest = {
      name: payload.name,
      orgUnitId: payload.orgUnitId,
      startYear: payload.startYear,
      durationYears: payload.durationYears,
    };
    this.planService.create(request).subscribe({
      next: plan => {
        this.toast.success('Strategic plan created.');
        this.dialogOpen.set(false);
        // Land on the new plan's detail page so the user can flesh it out.
        this.router.navigate(['/strategy/org-units', plan.orgUnitId]);
      },
      error: err => {
        this.dialogError.set(showErrorHere(err) ?? 'Failed to create plan.');
      },
    });
  }

  // ── Visual helpers ─────────────────────────────────────────────────────

  getInitials(text: string): string {
    return text
      .split(/\s+/)
      .filter(w => w.length > 0)
      .map(w => w[0]!.toUpperCase())
      .slice(0, 2)
      .join('');
  }

  getAvatarColor(name: string): string {
    const colors = [
      '#3b82f6', '#ef4444', '#f59e0b', '#10b981', '#8b5cf6',
      '#ec4899', '#06b6d4', '#f97316', '#6366f1', '#14b8a6',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    return colors[Math.abs(hash) % colors.length]!;
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
  }

  statusBadgeClass(status: StrategicPlanStatus): string {
    switch (status) {
      case StrategicPlanStatus.Draft: return 'st-badge--draft';
      case StrategicPlanStatus.PendingApproval: return 'st-badge--pending';
      case StrategicPlanStatus.Approved: return 'st-badge--approved';
      case StrategicPlanStatus.Archived: return 'st-badge--archived';
      default: return 'st-badge--archived';
    }
  }

  statusLabel(status: StrategicPlanStatus): string {
    switch (status) {
      case StrategicPlanStatus.Draft: return 'Draft';
      case StrategicPlanStatus.PendingApproval: return 'Pending approval';
      case StrategicPlanStatus.Approved: return 'Approved';
      case StrategicPlanStatus.Archived: return 'Archived';
      default: return 'Unknown';
    }
  }

  /**
   * Fallback when the plan's org unit is not in the user's scope (rare —
   * shouldn't happen since plans are loaded from those very units).
   */
  private synthesizeOrgUnit(plan: StrategicPlan): OrgUnit {
    return {
      id: plan.orgUnitId,
      name: plan.orgUnitName,
      code: plan.orgUnitCode,
      level: 1,
      isDeleted: false,
    } as OrgUnit;
  }
}
