import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Headline figure on the shared icon tile. Rendered as a button: every stat
 * on a dashboard leads somewhere (a drill-down list or a deeper dashboard),
 * so it is focusable and announces itself as actionable.
 */
@Component({
  selector: 'app-dash-stat',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="ds" [class.ds--danger]="tone() === 'danger'"
            [attr.title]="hint() || null" (click)="activate.emit()">
      <span class="ds__icon" aria-hidden="true"><i [class]="icon()"></i></span>
      <span class="ds__text">
        @if (loading()) {
          <span class="ds__skel"></span>
        } @else {
          <span class="ds__value">{{ value() }}</span>
        }
        <span class="ds__label">{{ label() }}</span>
      </span>
      <i class="bi bi-arrow-up-right ds__go" aria-hidden="true"></i>
    </button>
  `,
  styleUrl: './dash-stat.component.scss',
})
export class DashStatComponent {
  readonly icon    = input.required<string>();
  readonly label   = input.required<string>();
  readonly value   = input<string | number>('');
  /** Tooltip describing where the card leads. */
  readonly hint    = input<string>('');
  /** `danger` is reserved for a figure that flags a problem. */
  readonly tone    = input<'brand' | 'danger'>('brand');
  readonly loading = input(false);
  readonly activate = output<void>();
}
