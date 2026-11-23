import { HttpErrorResponse } from '@angular/common/http';
import {
  WEEK_DAY_LABELS,
  WEEK_DAYS_MONDAY_FIRST,
  WeekDayName,
  WorkCalendarDay,
  WorkCalendarException,
} from '../../../models/work-calendar.model';
import { formatTime, toDayNumber } from '../../../shared/utils/work-calendar.util';

/** Display helpers for the Calendars admin page. Pure functions, no state. */

const MONTHS_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
export const MONTHS_LONG = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

function dayRow(days: readonly WorkCalendarDay[], name: WeekDayName): WorkCalendarDay | undefined {
  return days.find(d => String(d.dayOfWeek).toLowerCase() === name);
}

/**
 * Working days as runs in Monday-first order, wrapping round the week:
 * "Mon–Fri", "Sun–Thu", "Mon–Thu, Sat", "Every day", "None".
 */
export function summarizeWorkingDays(days: readonly WorkCalendarDay[]): string {
  const flags = WEEK_DAYS_MONDAY_FIRST.map(name => !!dayRow(days, name)?.isWorkingDay);
  const count = flags.filter(Boolean).length;
  if (count === 0) return 'None';
  if (count === 7) return 'Every day';

  // Start just after an off day so a run never gets split by the week wrapping round.
  const start = flags.findIndex((working, i) => working && !flags[(i + 6) % 7]);
  const runs: string[] = [];
  let i = 0;
  while (i < 7) {
    const index = (start + i) % 7;
    if (!flags[index]) { i++; continue; }
    let length = 0;
    while (length < 7 && flags[(index + length) % 7]) length++;
    const first = WEEK_DAY_LABELS[WEEK_DAYS_MONDAY_FIRST[index]].short;
    const last  = WEEK_DAY_LABELS[WEEK_DAYS_MONDAY_FIRST[(index + length - 1) % 7]].short;
    runs.push(length === 1 ? first : length === 2 ? `${first}, ${last}` : `${first}–${last}`);
    i += length;
  }
  return runs.join(', ');
}

/** "08:00–17:00" when every working day shares the same hours, otherwise "Varies by day". */
export function summarizeHours(days: readonly WorkCalendarDay[]): string {
  const working = days.filter(d => d.isWorkingDay);
  if (working.length === 0) return '—';
  const labels = new Set(working.map(d => hoursLabel(d.startTime, d.endTime)));
  return labels.size === 1 ? [...labels][0] : 'Varies by day';
}

/** Per-day hours, Monday first — used as the tooltip when hours vary. */
export function hoursByDay(days: readonly WorkCalendarDay[]): string {
  return WEEK_DAYS_MONDAY_FIRST
    .map(name => {
      const row = dayRow(days, name);
      const label = row?.isWorkingDay ? hoursLabel(row.startTime, row.endTime) : 'off';
      return `${WEEK_DAY_LABELS[name].short} ${label}`;
    })
    .join(' · ');
}

export function hoursLabel(start?: string | null, end?: string | null): string {
  return start && end ? `${formatTime(start)}–${formatTime(end)}` : '—';
}

/** "2 Dec 2026", "2–3 Dec 2026", "28 Nov – 3 Dec 2026", "30 Dec 2026 – 5 Jan 2027". */
export function formatDateRange(start: string, end: string): string {
  const [sy, sm, sd] = start.split('-').map(Number);
  const [ey, em, ed] = end.split('-').map(Number);
  if (start === end) return `${sd} ${MONTHS_SHORT[sm - 1]} ${sy}`;
  if (sy === ey && sm === em) return `${sd}–${ed} ${MONTHS_SHORT[sm - 1]} ${sy}`;
  if (sy === ey) return `${sd} ${MONTHS_SHORT[sm - 1]} – ${ed} ${MONTHS_SHORT[em - 1]} ${sy}`;
  return `${sd} ${MONTHS_SHORT[sm - 1]} ${sy} – ${ed} ${MONTHS_SHORT[em - 1]} ${ey}`;
}

/** Number of calendar days in an inclusive range. */
export function rangeLength(start: string, end: string): number {
  return toDayNumber(end) - toDayNumber(start) + 1;
}

export function exceptionKindLabel(e: Pick<WorkCalendarException, 'isWorkingDay' | 'startTime' | 'endTime'>): string {
  if (!e.isWorkingDay) return 'Off';
  return e.startTime && e.endTime ? 'Working, changed hours' : 'Working';
}

/** The first exception that has not ended yet, by start date. */
export function nextException(
  exceptions: readonly WorkCalendarException[],
  today: string,
): WorkCalendarException | null {
  return [...exceptions]
    .filter(e => e.endDate >= today)
    .sort((a, b) => a.startDate.localeCompare(b.startDate))[0] ?? null;
}

/**
 * Reads the reason out of an API error: `ApiResponse.message` (400/404/409 from
 * the controller) or the FluentValidation `ProblemDetails.errors` dictionary.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const body = (err as HttpErrorResponse | undefined)?.error;
  if (body && typeof body === 'object') {
    if (typeof body.message === 'string' && body.message.trim()) return body.message;
    if (Array.isArray(body.errors) && body.errors.length) return body.errors.join(' ');
    if (body.errors && typeof body.errors === 'object') {
      const messages = Object.values(body.errors as Record<string, string[]>).flat().filter(Boolean);
      if (messages.length) return [...new Set(messages)].join(' ');
    }
    if (typeof body.detail === 'string' && body.detail.trim()) return body.detail;
    if (typeof body.title === 'string' && body.title.trim()) return body.title;
  }
  if (typeof body === 'string' && body.trim()) return body;
  return fallback;
}
