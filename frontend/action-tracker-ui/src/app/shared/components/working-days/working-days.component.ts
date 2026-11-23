import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { WorkspaceCalendarRules } from '../../../services/workspace-calendar.service';
import { workingDayCount } from '../../validators/working-day.validator';

/**
 * "12 working days" under a start/end date pair — the duration in working days
 * of the workspace's calendar, both ends counted (WO-2026-11 C-3, Q7).
 * Shows nothing until both dates are set and the calendar is loaded.
 */
@Component({
  selector: 'app-working-days',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (count(); as c) {
      <div class="wd-count">
        <i class="bi bi-calendar-week" aria-hidden="true"></i>
        {{ c }} working day{{ c === 1 ? '' : 's' }}
        @if (calendarName(); as name) { <span class="wd-count__calendar">· {{ name }}</span> }
      </div>
    } @else if (count() === 0) {
      <div class="wd-count">
        <i class="bi bi-calendar-week" aria-hidden="true"></i>
        No working days in this range
      </div>
    }
  `,
  styles: [`
    .wd-count {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.25rem;
      margin-top: 0.25rem;
      font-size: 0.8125rem;
      color: var(--t2);
    }
    .wd-count__calendar { color: var(--t3); }
  `],
})
export class WorkingDaysComponent {
  readonly calendar = input<WorkspaceCalendarRules | null>(null);
  readonly start = input<string | null | undefined>(null);
  readonly end = input<string | null | undefined>(null);
  /** Show the calendar's name after the count. */
  readonly showCalendar = input(true);

  readonly count = computed(() => workingDayCount(this.calendar(), this.start(), this.end()));
  readonly calendarName = computed(() =>
    this.showCalendar() ? this.calendar()?.calendar.calendarName ?? null : null);
}
