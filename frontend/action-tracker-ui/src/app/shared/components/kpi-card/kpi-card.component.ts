import { Component, input, computed } from '@angular/core';

@Component({
  selector: 'app-kpi-card',
  standalone: true,
  templateUrl: './kpi-card.component.html',
  styleUrl: './kpi-card.component.scss',
})
export class KpiCardComponent {
  readonly title = input.required<string>();
  readonly value = input.required<string | number>();
  readonly subtitle = input<string>('');
  readonly icon = input<string>('');
  readonly trend = input<number | null>(null);
  readonly colorClass = input<string>('primary');

  readonly trendPositive = computed(() => (this.trend() ?? 0) >= 0);
  readonly trendAbs = computed(() => Math.abs(this.trend() ?? 0));
  readonly hasTrend = computed(() => this.trend() !== null);
}
