import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, catchError, forkJoin, of } from 'rxjs';
import { WorkflowService } from './workflow.service';
import { ProjectWorkflowService } from './project-workflow.service';
import { StrategicPlanWorkflowService } from './strategic-plan-workflow.service';
import { KpiActualsService } from './kpi-actuals.service';

@Injectable({ providedIn: 'root' })
export class WorkflowStateService {
  private readonly workflowService = inject(WorkflowService);
  private readonly projectWorkflowService = inject(ProjectWorkflowService);
  private readonly strategicPlanWorkflowService = inject(StrategicPlanWorkflowService);
  private readonly kpiActualsService = inject(KpiActualsService);

  private readonly _pendingCount$ = new BehaviorSubject<number>(0);
  readonly pendingCount$ = this._pendingCount$.asObservable();

  refreshPendingCount(): void {
    forkJoin({
      actionItem: this.workflowService.getPendingSummary(),
      project: this.projectWorkflowService.getPendingSummary(),
      // Users without StrategicPlans.View receive 403; treat as 0.
      strategicPlan: this.strategicPlanWorkflowService.getPendingSummary().pipe(
        catchError(() => of({ success: false, data: { pendingPlanApprovals: 0 } } as any)),
      ),
      // Users without KpiActuals.View receive 403; treat as 0.
      kpiActuals: this.kpiActualsService.getPendingSummary().pipe(
        catchError(() => of({ success: false, data: { pendingKpiActuals: 0 } } as any)),
      ),
    }).subscribe({
      next: ({ actionItem, project, strategicPlan, kpiActuals }) => {
        let total = 0;
        if (actionItem.success) total += actionItem.data.totalPending;
        if (project.success) total += project.data.pendingProjectApprovals;
        if (strategicPlan?.success) total += strategicPlan.data.pendingPlanApprovals;
        if (kpiActuals?.success) total += kpiActuals.data.pendingKpiActuals;
        this._pendingCount$.next(total);
      },
      error: () => {},
    });
  }

  clearPendingCount(): void {
    this._pendingCount$.next(0);
  }
}
