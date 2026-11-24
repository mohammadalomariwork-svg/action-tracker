import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { subItemProgressLabel } from '../../utils/sub-item-dates.util';

/**
 * `<app-sub-item-count [done]="item.subItemDoneCount" [total]="item.subItemCount" />`
 * — "3 of 5 done" chip for an action item's sub-items (WO-2026-11 A-2).
 * Renders nothing for an item without sub-items. `mine` adds "· 2 yours"
 * (My Actions → Assigned through a sub-item).
 */
@Component({
  selector: 'app-sub-item-count',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (total()) {
      <span class="sic" [class.sic--complete]="done() === total()"
            [attr.title]="title()" [attr.aria-label]="title()">
        <i class="bi bi-list-check" aria-hidden="true"></i>
        <span aria-hidden="true">{{ label() }}@if (mine()) { · {{ mine() }} yours }</span>
      </span>
    }
  `,
  styles: [`
    :host { display: inline-flex; }
    .sic {
      display: inline-flex; align-items: center; gap: 0.25rem;
      padding: 0 0.45rem; border-radius: 999px;
      border: 1px solid var(--bd); background: var(--el);
      color: var(--t1); font-size: 11px; font-weight: 600; line-height: 1.7; white-space: nowrap;
    }
    .sic i { color: var(--accent); }
    .sic--complete { border-color: rgba(var(--okr), 0.5); background: rgba(var(--okr), 0.15); }
    .sic--complete i { color: var(--t1); }
  `],
})
export class SubItemCountComponent {
  readonly done  = input<number | null | undefined>(0);
  readonly total = input<number | null | undefined>(0);
  /** How many of the sub-items are the viewer's; shown when set. */
  readonly mine  = input<number | null | undefined>(null);

  readonly label = computed(() => subItemProgressLabel(this.done(), this.total()));
  readonly title = computed(() => {
    const base = `${this.done() ?? 0} of ${this.total()} sub-items done`;
    return this.mine() ? `${base}; ${this.mine()} assigned to you` : base;
  });
}
