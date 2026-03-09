import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, computed,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';

import { ActionItemService }     from '../../../core/services/action-item.service';
import { ToastService }          from '../../../core/services/toast.service';

import {
  ActionItem, ActionStatus, ActionPriority,
} from '../../../core/models/action-item.model';

import { StatusBadgeComponent }   from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { ProgressBarComponent }   from '../../../shared/components/progress-bar/progress-bar.component';
import { PageHeaderComponent }    from '../../../shared/components/page-header/page-header.component';
import { CommentsSectionComponent }  from '../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../shared/components/documents-section/documents-section.component';
import { BreadcrumbComponent }       from '../../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-action-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink, DatePipe,
    StatusBadgeComponent, PriorityBadgeComponent,
    ProgressBarComponent, PageHeaderComponent,
    CommentsSectionComponent, DocumentsSectionComponent,
    BreadcrumbComponent,
  ],
  templateUrl: './action-detail.component.html',
  styleUrl:    './action-detail.component.scss',
})
export class ActionDetailComponent implements OnInit {
  private readonly route      = inject(ActivatedRoute);
  private readonly router     = inject(Router);
  private readonly actionSvc  = inject(ActionItemService);
  private readonly toastSvc   = inject(ToastService);

  readonly item       = signal<ActionItem | null>(null);
  readonly loading    = signal(true);

  readonly ActionStatus   = ActionStatus;
  readonly ActionPriority = ActionPriority;

  readonly workspaceId = computed(() => this.item()?.workspaceId ?? '');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadItem(id);
  }

  // ── Data loading ───────────────────────────────────────
  private loadItem(id: string): void {
    this.loading.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        this.item.set(r.data);
        this.loading.set(false);
      },
      error: () => {
        this.toastSvc.error('Failed to load action item.');
        this.loading.set(false);
      },
    });
  }

  // ── Helpers ────────────────────────────────────────────
  assigneeNames(): string {
    return this.item()?.assignees?.map(a => a.fullName).join(', ') || '—';
  }

  dueDateClass(): string {
    const i = this.item();
    if (!i) return '';
    if (i.isOverdue || i.status === ActionStatus.Overdue) return 'due--overdue';
    if (i.daysUntilDue <= 3) return 'due--warning';
    return 'due--ok';
  }
}
