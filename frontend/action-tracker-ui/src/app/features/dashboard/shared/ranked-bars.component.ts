import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

export type BarTone = 'brand' | 'good' | 'warn' | 'bad' | 'neutral';

export interface RankedSegment {
  value: number;
  tone: BarTone;
  label: string;
}

export interface RankedItem {
  id: string;
  label: string;
  sublabel?: string;
  /** Sets the bar length relative to the longest item. */
  total: number;
  segments: RankedSegment[];
  /** Text shown at the end of the row; defaults to the total. */
  trailing?: string;
}

/**
 * A list sorted by the caller, each row a stacked bar scaled against the
 * largest total. Rows are buttons — selecting one drills into it.
 */
@Component({
  selector: 'app-ranked-bars',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ranked-bars.component.html',
  styleUrl: './ranked-bars.component.scss',
})
export class RankedBarsComponent {
  readonly items     = input.required<RankedItem[]>();
  /** Rows shown before "Show all". */
  readonly limit     = input(8);
  /** Show the rank number and an initial avatar (for people). */
  readonly avatars   = input(false);
  readonly emptyText = input('Nothing to show yet.');
  readonly pick      = output<RankedItem>();

  readonly expanded = signal(false);

  readonly max = computed(() => Math.max(1, ...this.items().map(i => i.total)));

  readonly visible = computed(() =>
    this.expanded() ? this.items() : this.items().slice(0, this.limit()));

  /** Tones present anywhere in the list, for the key above it. */
  readonly key = computed(() => {
    const seen = new Map<BarTone, string>();
    for (const item of this.items())
      for (const seg of item.segments)
        if (!seen.has(seg.tone)) seen.set(seg.tone, seg.label);
    return [...seen.entries()].map(([tone, label]) => ({ tone, label }));
  });

  width(value: number): number {
    return (value / this.max()) * 100;
  }

  initial(label: string): string {
    return (label.trim().charAt(0) || '?').toUpperCase();
  }

  describe(item: RankedItem): string {
    const parts = item.segments.filter(s => s.value > 0).map(s => `${s.value} ${s.label.toLowerCase()}`);
    return `${item.label}: ${item.total} total` + (parts.length ? ` — ${parts.join(', ')}` : '');
  }
}
