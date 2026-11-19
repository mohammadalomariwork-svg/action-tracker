import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface LegendItem {
  key: string;
  label: string;
  count: number;
  color: string;
}

/**
 * Chart legend as a list of buttons. A canvas cannot be reached by keyboard
 * or screen reader, so this is the accessible route to the same drill-down
 * as clicking a segment — and it shows the exact figures.
 */
@Component({
  selector: 'app-dash-legend',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="dl" [class.dl--stack]="stacked()">
      @for (item of items(); track item.key) {
        <li>
          <button type="button" class="dl__item" [disabled]="item.count === 0"
                  [attr.title]="'Show ' + item.label" (click)="pick.emit(item.key)">
            <span class="dl__dot" [style.background]="item.color" aria-hidden="true"></span>
            <span class="dl__label">{{ item.label }}</span>
            <span class="dl__count">{{ item.count }}</span>
          </button>
        </li>
      }
    </ul>
  `,
  styleUrl: './dash-legend.component.scss',
})
export class DashLegendComponent {
  readonly items   = input.required<LegendItem[]>();
  /** One item per line (beside a doughnut) instead of wrapping chips. */
  readonly stacked = input(false);
  readonly pick    = output<string>();
}
