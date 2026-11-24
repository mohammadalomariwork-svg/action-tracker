import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { actualDatesProblem } from '../../utils/actual-dates.util';

/**
 * Actual start / actual end inputs for an action item (WO-2026-11 A-1).
 * Bootstrap row of two date fields; values are `yyyy-MM-dd`.
 *
 *  - Never disabled by the planned-date freeze, no approval, no working-day hint.
 *  - Actual end is editable only when `endEditable` (the item is Done, or moves
 *    to Done in this save and the move applies directly); otherwise it is shown
 *    read-only with a note on when it is recorded.
 *  - Shows the first rule a value breaks (`actual-dates.util.ts`); the host
 *    repeats that check before saving and the server decides.
 *
 * ```html
 * <app-actual-dates-fields idPrefix="al-actual" [(start)]="form.actualStartDate" [(end)]="form.actualEndDate"
 *                          [endEditable]="actualEndEditable()" [standalone]="item.isStandalone" [today]="today()" />
 * ```
 * Input ids are `{idPrefix}-start` / `{idPrefix}-end`, hints `{id}-hint`.
 */
@Component({
  selector: 'app-actual-dates-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="row g-2 mb-3">
      <div class="col-12 col-sm-6">
        <label class="form-label" [for]="idPrefix() + '-start'">Actual start</label>
        <input [id]="idPrefix() + '-start'" type="date" class="form-control"
               [value]="start()" (input)="start.set(valueOf($event))" [max]="today()"
               [attr.aria-describedby]="idPrefix() + '-start-hint'" />
        <small [id]="idPrefix() + '-start-hint'" class="adf-hint">
          {{ startHint() ?? 'Recorded when the item moves to In Progress.' }}
        </small>
      </div>
      <div class="col-12 col-sm-6">
        <label class="form-label" [for]="idPrefix() + '-end'">Actual end</label>
        @if (endEditable()) {
          <input [id]="idPrefix() + '-end'" type="date" class="form-control"
                 [value]="end()" (input)="end.set(valueOf($event))" [min]="start() || ''" [max]="today()"
                 [attr.aria-describedby]="idPrefix() + '-end-hint'" />
          <small [id]="idPrefix() + '-end-hint'" class="adf-hint">{{ endHint() ?? 'Leave empty to record today.' }}</small>
        } @else {
          <input [id]="idPrefix() + '-end'" type="date" class="form-control" [value]="end()" disabled
                 [attr.aria-describedby]="idPrefix() + '-end-hint'" />
          <small [id]="idPrefix() + '-end-hint'" class="adf-hint">
            {{ standalone() ? 'Recorded when the move to Done is approved.' : 'Recorded when the item moves to Done.' }}
          </small>
        }
      </div>
      @if (problem(); as p) {
        <div class="col-12">
          <div class="adf-problem" role="alert">
            <i class="bi bi-calendar-x adf-problem__icon" aria-hidden="true"></i>{{ p }}
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .adf-hint {
      display: block;
      margin-top: 0.25rem;
      font-size: 0.8125rem;
      line-height: 1.35;
      color: var(--t2);
    }
    /* Same treatment as the working-day hint: body text in --t1 on a light
       --err tint, because the red alone falls under 4.5:1 on the dark surface. */
    .adf-problem {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
      padding: 0.375rem 0.5rem;
      border-left: 3px solid var(--err);
      border-radius: var(--r-sm, 0.25rem);
      background: rgba(var(--errr), 0.1);
      font-size: 0.8125rem;
      line-height: 1.35;
      color: var(--t1);
    }
    .adf-problem__icon { color: var(--err); }
  `],
})
export class ActualDatesFieldsComponent {
  /** Prefix for the input ids (unique per page). */
  readonly idPrefix    = input('actual');
  readonly start       = model('');
  readonly end         = model('');
  /** True when this save may set the actual end. */
  readonly endEditable = input(false);
  /** Standalone item: a move to Done goes for approval first. */
  readonly standalone  = input(false);
  /** Today in the workspace calendar's time zone (`todayInTimeZone`). */
  readonly today       = input.required<string>();
  /** Overrides the note under Actual start. */
  readonly startHint   = input<string | null>(null);
  /** Overrides the note under an editable Actual end. */
  readonly endHint     = input<string | null>(null);

  readonly problem = computed(() => actualDatesProblem({
    start: this.start(),
    end: this.endEditable() ? this.end() : '',
    doneAfterSave: this.endEditable(),
    today: this.today(),
  }));

  valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
