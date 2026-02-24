import { Component, input, computed } from '@angular/core';
import { ActionPriority } from '../../../core/models/action-item.model';

interface BadgeConfig { label: string; cssClass: string; }

const PRIORITY_CONFIG: Record<ActionPriority, BadgeConfig> = {
  [ActionPriority.Critical]: { label: 'Critical', cssClass: 'badge--red'    },
  [ActionPriority.High]:     { label: 'High',     cssClass: 'badge--orange' },
  [ActionPriority.Medium]:   { label: 'Medium',   cssClass: 'badge--yellow' },
  [ActionPriority.Low]:      { label: 'Low',      cssClass: 'badge--green'  },
};

@Component({
  selector: 'app-priority-badge',
  standalone: true,
  templateUrl: './priority-badge.component.html',
  styleUrl: './priority-badge.component.scss',
})
export class PriorityBadgeComponent {
  readonly priority = input.required<ActionPriority>();

  readonly config = computed(() => PRIORITY_CONFIG[this.priority()] ?? { label: 'Unknown', cssClass: 'badge--gray' });
}
