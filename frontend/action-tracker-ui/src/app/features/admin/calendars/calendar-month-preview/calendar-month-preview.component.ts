import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import {
  WEEK_DAY_LABELS,
  WEEK_DAYS_MONDAY_FIRST,
  WEEK_DAY_NAMES,
  WorkCalendarDay,
  WorkCalendarException,
} from '../../../../models/work-calendar.model';
import {
  CalendarDayKind,
  ResolvedCalendarDay,
  addDays,
  createCalendarRules,
  dayOfWeekOf,
  formatTime,
  todayIso,
} from '../../../../shared/utils/work-calendar.util';
import { MONTHS_LONG } from '../calendar-ui';

interface PreviewCell {
  day: ResolvedCalendarDay;
  dayOfMonth: number;
  isToday: boolean;
  /** Visible line under the day number (hours or exception name). */
  note: string;
  /** Full description for screen readers and the tooltip. */
  label: string;
}

const KIND_LABELS: Record<CalendarDayKind, string> = {
  working: 'Working day',
  off:     'Off day',
  holiday: 'Holiday / closure',
  special: 'Special working day or changed hours',
};

/**
 * One month of a calendar, coloured with the same rules the server applies
 * (`shared/utils/work-calendar.util.ts` ↔ `ResolvedWorkCalendar`). Fed the
 * editor's current, unsaved values, so it shows the effect of an edit before Save.
 */
@Component({
  selector: 'app-calendar-month-preview',
  standalone: true,
  templateUrl: './calendar-month-preview.component.html',
  styleUrl: './calendar-month-preview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CalendarMonthPreviewComponent {
  readonly days       = input.required<WorkCalendarDay[]>();
  readonly exceptions = input.required<WorkCalendarException[]>();
  /** `yyyy-MM`; two-way so the editor can jump to an exception's month. */
  readonly month      = model<string>(todayIso().slice(0, 7));

  readonly weekDays = WEEK_DAYS_MONDAY_FIRST.map(name => WEEK_DAY_LABELS[name]);
  readonly kindLabels = KIND_LABELS;
  readonly kinds: CalendarDayKind[] = ['working', 'off', 'holiday', 'special'];

  private readonly rules = computed(() => createCalendarRules(this.days(), this.exceptions()));

  readonly firstDay = computed(() => `${this.month()}-01`);

  readonly lastDay = computed(() => {
    const [y, m] = this.month().split('-').map(Number);
    const next = m === 12 ? `${y + 1}-01-01` : `${y}-${String(m + 1).padStart(2, '0')}-01`;
    return addDays(next, -1);
  });

  readonly monthLabel = computed(() => {
    const [y, m] = this.month().split('-').map(Number);
    return `${MONTHS_LONG[m - 1]} ${y}`;
  });

  /** Weeks, Monday first; `null` pads the days outside the month. */
  readonly weeks = computed<(PreviewCell | null)[][]>(() => {
    const rules = this.rules();
    const first = this.firstDay();
    const last  = this.lastDay();
    const today = todayIso();
    const lead  = (dayOfWeekOf(first) + 6) % 7;   // Monday = 0

    const cells: (PreviewCell | null)[] = Array.from({ length: lead }, () => null);
    for (let date = first; date <= last; date = addDays(date, 1)) {
      const day = rules.resolve(date);
      cells.push({
        day,
        dayOfMonth: Number(date.slice(8, 10)),
        isToday: date === today,
        note: this.noteFor(day),
        label: this.labelFor(day),
      });
    }
    while (cells.length % 7 !== 0) cells.push(null);

    const weeks: (PreviewCell | null)[][] = [];
    for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
    return weeks;
  });

  readonly counts = computed<Record<CalendarDayKind, number>>(() => {
    const counts: Record<CalendarDayKind, number> = { working: 0, off: 0, holiday: 0, special: 0 };
    for (const week of this.weeks()) {
      for (const cell of week) if (cell) counts[cell.day.kind]++;
    }
    return counts;
  });

  readonly workingDays = computed(() => this.rules().workingDaysBetween(this.firstDay(), this.lastDay()));

  previous(): void { this.shift(-1); }
  next(): void     { this.shift(1); }
  thisMonth(): void { this.month.set(todayIso().slice(0, 7)); }

  isCurrentMonth(): boolean {
    return this.month() === todayIso().slice(0, 7);
  }

  private shift(by: number): void {
    const [y, m] = this.month().split('-').map(Number);
    const index = y * 12 + (m - 1) + by;
    const year  = Math.floor(index / 12);
    const month = (index % 12) + 1;
    this.month.set(`${year}-${String(month).padStart(2, '0')}`);
  }

  private noteFor(day: ResolvedCalendarDay): string {
    if (day.exception) return day.exception.name;
    return day.hours ? `${formatTime(day.hours.start)}–${formatTime(day.hours.end)}` : '';
  }

  private labelFor(day: ResolvedCalendarDay): string {
    const [y, m, d] = day.date.split('-').map(Number);
    const weekday = WEEK_DAY_LABELS[WEEK_DAY_NAMES[day.dayOfWeek]].long;
    const parts = [`${weekday} ${d} ${MONTHS_LONG[m - 1]} ${y}`, KIND_LABELS[day.kind]];
    if (day.exception) parts.push(day.exception.name);
    if (day.hours) parts.push(`${formatTime(day.hours.start)} to ${formatTime(day.hours.end)}`);
    return parts.join(', ');
  }
}
