import {
  ChangeDetectionStrategy, Component, HostListener, effect, inject, input, output, signal, untracked,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';

import { ActionItemService } from '../../../core/services/action-item.service';
import { ActionItem, ActionItemFilter, ActionStatus } from '../../../core/models/action-item.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PriorityBadgeComponent } from '../../../shared/components/priority-badge/priority-badge.component';
import { DrilldownRequest } from './drilldown.model';

const PAGE_SIZE = 20;

/**
 * Right-hand drawer that lists what is behind a chart segment or stat card.
 * Every row links to that item's own page, so a dashboard number can always
 * be followed down to the records that make it up.
 */
@Component({
  selector: 'app-drilldown-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, StatusBadgeComponent, PriorityBadgeComponent],
  templateUrl: './drilldown-panel.component.html',
  styleUrl: './drilldown-panel.component.scss',
})
export class DrilldownPanelComponent {
  private readonly actionSvc = inject(ActionItemService);

  /** What to show; null closes the drawer. */
  readonly request = input<DrilldownRequest | null>(null);
  readonly closed  = output<void>();

  readonly actions    = signal<ActionItem[]>([]);
  readonly total      = signal(0);
  readonly loading    = signal(false);
  readonly failed     = signal(false);
  private  pageNumber = 1;
  /** Guards against a slow response landing after a newer request. */
  private  loadToken  = 0;

  readonly ActionStatus = ActionStatus;

  constructor() {
    effect(() => {
      const req = this.request();
      untracked(() => {
        this.actions.set([]);
        this.total.set(0);
        this.failed.set(false);
        this.pageNumber = 1;
        this.loadToken++;
        if (req?.kind === 'actions') this.loadPage();
        else if (req?.kind === 'rows') this.total.set(req.rows.length);
      });
    });
  }

  get hasMore(): boolean {
    return this.actions().length < this.total();
  }

  loadMore(): void {
    this.pageNumber++;
    this.loadPage();
  }

  retry(): void {
    this.loadPage();
  }

  private loadPage(): void {
    const req = this.request();
    if (req?.kind !== 'actions') return;

    const token  = ++this.loadToken;
    const filter: ActionItemFilter = {
      ...req.filter,
      pageNumber: this.pageNumber,
      pageSize: PAGE_SIZE,
      sortBy: 'dueDate',
      sortDescending: false,
    };

    this.loading.set(true);
    this.failed.set(false);
    const call = req.scope === 'mine' ? this.actionSvc.getMyActions(filter) : this.actionSvc.getAll(filter);

    call.subscribe({
      next: r => {
        if (token !== this.loadToken) return;
        const page = r.data;
        this.actions.update(list => this.pageNumber === 1 ? page.items : [...list, ...page.items]);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: () => {
        if (token !== this.loadToken) return;
        if (this.pageNumber > 1) this.pageNumber--;
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  assignees(item: ActionItem): string {
    const list = item.assignees ?? [];
    if (list.length === 0) return 'Unassigned';
    return list.length === 1 ? list[0].fullName : `${list[0].fullName} +${list.length - 1}`;
  }

  dueNote(item: ActionItem): string {
    if (item.status === ActionStatus.Done) return '';
    if (item.isOverdue || item.daysUntilDue < 0) return `${Math.abs(item.daysUntilDue)}d overdue`;
    if (item.daysUntilDue === 0) return 'Due today';
    if (item.daysUntilDue === 1) return 'Due tomorrow';
    return `in ${item.daysUntilDue}d`;
  }

  isLate(item: ActionItem): boolean {
    return item.status !== ActionStatus.Done && (item.isOverdue || item.daysUntilDue < 0);
  }

  close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.request()) this.close();
  }
}
