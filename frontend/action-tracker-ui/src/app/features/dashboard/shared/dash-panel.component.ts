import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Titled card that holds one chart, list or table. */
@Component({
  selector: 'app-dash-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="dp">
      <header class="dp__head">
        @if (icon()) {
          <span class="dp__icon" aria-hidden="true"><i [class]="icon()"></i></span>
        }
        <div class="dp__titles">
          <h2 class="dp__title">{{ title() }}</h2>
          @if (subtitle()) {
            <p class="dp__sub">{{ subtitle() }}</p>
          }
        </div>
        <div class="dp__actions"><ng-content select="[panelActions]" /></div>
      </header>
      <div class="dp__body"><ng-content /></div>
    </section>
  `,
  styleUrl: './dash-panel.component.scss',
})
export class DashPanelComponent {
  readonly title    = input.required<string>();
  readonly subtitle = input<string>('');
  readonly icon     = input<string>('');
}
