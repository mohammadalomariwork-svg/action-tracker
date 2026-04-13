import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, forkJoin } from 'rxjs';
import { WorkflowService } from './workflow.service';
import { ProjectWorkflowService } from './project-workflow.service';

@Injectable({ providedIn: 'root' })
export class WorkflowStateService {
  private readonly workflowService = inject(WorkflowService);
  private readonly projectWorkflowService = inject(ProjectWorkflowService);

  private readonly _pendingCount$ = new BehaviorSubject<number>(0);
  readonly pendingCount$ = this._pendingCount$.asObservable();

  refreshPendingCount(): void {
    forkJoin({
      actionItem: this.workflowService.getPendingSummary(),
      project: this.projectWorkflowService.getPendingSummary(),
    }).subscribe({
      next: ({ actionItem, project }) => {
        let total = 0;
        if (actionItem.success) total += actionItem.data.totalPending;
        if (project.success) total += project.data.pendingProjectApprovals;
        this._pendingCount$.next(total);
      },
      error: () => {},
    });
  }

  clearPendingCount(): void {
    this._pendingCount$.next(0);
  }
}
