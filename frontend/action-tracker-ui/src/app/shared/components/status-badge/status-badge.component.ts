import { Component, input, computed } from '@angular/core';
import { ActionStatus } from '../../../core/models/action-item.model';

interface BadgeConfig { label: string; cssClass: string; }

const STATUS_CONFIG: Record<ActionStatus, BadgeConfig> = {
  [ActionStatus.ToDo]:      { label: 'To Do',       cssClass: 'badge--gray'   },
  [ActionStatus.InProgress]:{ label: 'In Progress',  cssClass: 'badge--blue'   },
  [ActionStatus.InReview]:  { label: 'In Review',    cssClass: 'badge--purple' },
  [ActionStatus.Done]:      { label: 'Done',         cssClass: 'badge--green'  },
  [ActionStatus.Overdue]:   { label: 'Overdue',      cssClass: 'badge--red'    },
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss',
})
export class StatusBadgeComponent {
  readonly status = input.required<ActionStatus>();

  readonly config = computed(() => STATUS_CONFIG[this.status()] ?? { label: 'Unknown', cssClass: 'badge--gray' });
}
