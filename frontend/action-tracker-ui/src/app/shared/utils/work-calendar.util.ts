/**
 * Working-calendar rules on the client.
 *
 * Mirrors `ResolvedWorkCalendar` in
 * `ActionTracker.Application/Features/Calendars/Services/ResolvedWorkCalendar.cs`
 * so the Calendars admin page can preview a calendar — including unsaved edits —
 * exactly as the server will evaluate it. Change both sides together.
 *
 * Rules (same as the server):
 *  - An exception covering a date overrides the weekly pattern for that date.
 *  - When several exceptions cover the same date the shortest wins; on a tie a
 *    non-working exception wins.
 *  - A day of week with no row is non-working.
 *  - Non-working days and holidays carry no hours; a working exception without
 *    hours keeps the weekly pattern's hours.
 *
 * Dates are ISO `yyyy-MM-dd` strings and all arithmetic is done in whole UTC days,
 * so the browser's time zone never shifts a date.
 *
 * Only type imports here: the file stays free of Angular and runtime imports.
 */
import type { WorkCalendarDay, WorkCalendarException } from '../../models/work-calendar.model';

const MS_PER_DAY = 86_400_000;

const DAY_INDEX: Record<string, number> = {
  sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6,
};

/** How a date reads in the preview. */
export type CalendarDayKind =
  /** Working day from the weekly pattern. */
  | 'working'
  /** Off day from the weekly pattern (e.g. the weekend). */
  | 'off'
  /** Non-working exception: holiday or closure. */
  | 'holiday'
  /** Working exception: special working day or changed hours. */
  | 'special';

export interface WorkingHours {
  /** `HH:mm:ss` */
  start: string;
  end: string;
}

export interface ResolvedCalendarDay {
  /** `yyyy-MM-dd` */
  date: string;
  /** 0 = Sunday … 6 = Saturday */
  dayOfWeek: number;
  isWorkingDay: boolean;
  kind: CalendarDayKind;
  hours: WorkingHours | null;
  /** The exception that decided this date, if any. */
  exception: WorkCalendarException | null;
  /** What the weekly pattern alone says about this date. */
  weeklyIsWorkingDay: boolean;
}

export interface CalendarRules {
  resolve(date: string): ResolvedCalendarDay;
  isWorkingDay(date: string): boolean;
  getWorkingHours(date: string): WorkingHours | null;
  /** Working days from `start` to `end`, both ends counted; 0 when `end` is before `start`. */
  workingDaysBetween(start: string, end: string): number;
}

interface DayRule {
  isWorkingDay: boolean;
  hours: WorkingHours | null;
}

interface ExceptionRule {
  start: number;
  end: number;
  source: WorkCalendarException;
  hours: WorkingHours | null;
}

/** Builds the rules for a weekly pattern plus its exceptions. Inputs are not modified. */
export function createCalendarRules(
  days: readonly WorkCalendarDay[],
  exceptions: readonly WorkCalendarException[],
): CalendarRules {
  const week: DayRule[] = Array.from({ length: 7 }, () => ({ isWorkingDay: false, hours: null }));
  for (const day of days ?? []) {
    const index = DAY_INDEX[String(day.dayOfWeek).toLowerCase()];
    if (index === undefined) continue;
    week[index] = {
      isWorkingDay: !!day.isWorkingDay,
      hours: day.isWorkingDay ? hoursOf(day.startTime, day.endTime) : null,
    };
  }

  const rules: ExceptionRule[] = (exceptions ?? [])
    .filter(e => isIsoDate(e.startDate) && isIsoDate(e.endDate))
    .map(e => ({
      start: toDayNumber(e.startDate),
      end: toDayNumber(e.endDate),
      source: e,
      hours: e.isWorkingDay ? hoursOf(e.startTime, e.endTime) : null,
    }))
    // Most specific first: shortest range, then non-working before working.
    // Array.prototype.sort is stable, matching LINQ OrderBy/ThenBy.
    .sort((a, b) =>
      (a.end - a.start) - (b.end - b.start)
      || Number(a.source.isWorkingDay) - Number(b.source.isWorkingDay));

  const findException = (dayNumber: number): ExceptionRule | null => {
    for (const rule of rules) {
      if (dayNumber >= rule.start && dayNumber <= rule.end) return rule;
    }
    return null;
  };

  const resolveDayNumber = (dayNumber: number): ResolvedCalendarDay => {
    const dayOfWeek = dayOfWeekOfDayNumber(dayNumber);
    const weekly    = week[dayOfWeek];
    const rule      = findException(dayNumber);
    const date      = fromDayNumber(dayNumber);

    if (!rule) {
      return {
        date, dayOfWeek,
        isWorkingDay: weekly.isWorkingDay,
        kind: weekly.isWorkingDay ? 'working' : 'off',
        hours: weekly.isWorkingDay ? weekly.hours : null,
        exception: null,
        weeklyIsWorkingDay: weekly.isWorkingDay,
      };
    }

    const working = !!rule.source.isWorkingDay;
    return {
      date, dayOfWeek,
      isWorkingDay: working,
      kind: working ? 'special' : 'holiday',
      // A working exception without hours of its own keeps the weekly hours.
      hours: working ? (rule.hours ?? weekly.hours) : null,
      exception: rule.source,
      weeklyIsWorkingDay: weekly.isWorkingDay,
    };
  };

  return {
    resolve: date => resolveDayNumber(toDayNumber(date)),
    isWorkingDay: date => resolveDayNumber(toDayNumber(date)).isWorkingDay,
    getWorkingHours: date => resolveDayNumber(toDayNumber(date)).hours,
    workingDaysBetween: (start, end) => {
      const from = toDayNumber(start);
      const to   = toDayNumber(end);
      let count = 0;
      for (let n = from; n <= to; n++) {
        if (resolveDayNumber(n).isWorkingDay) count++;
      }
      return count;
    },
  };
}

// ── Dates (ISO yyyy-MM-dd, whole UTC days) ────────────────────────────────────

export function isIsoDate(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value);
}

/** Days since 1970-01-01. */
export function toDayNumber(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number);
  const date = new Date(0);
  date.setUTCFullYear(y, m - 1, d);   // setUTCFullYear keeps years below 100 literal
  date.setUTCHours(0, 0, 0, 0);
  return Math.round(date.getTime() / MS_PER_DAY);
}

export function fromDayNumber(dayNumber: number): string {
  const date = new Date(dayNumber * MS_PER_DAY);
  return `${String(date.getUTCFullYear()).padStart(4, '0')}-${pad2(date.getUTCMonth() + 1)}-${pad2(date.getUTCDate())}`;
}

export function addDays(iso: string, days: number): string {
  return fromDayNumber(toDayNumber(iso) + days);
}

/** 0 = Sunday … 6 = Saturday. */
export function dayOfWeekOf(iso: string): number {
  return dayOfWeekOfDayNumber(toDayNumber(iso));
}

/** Today's date in the browser's own time zone. */
export function todayIso(): string {
  const now = new Date();
  return `${now.getFullYear()}-${pad2(now.getMonth() + 1)}-${pad2(now.getDate())}`;
}

// ── Times (wire HH:mm:ss, <input type="time"> HH:mm) ──────────────────────────

/** `08:00:00` → `08:00` for a time input; keeps seconds when they are not zero. */
export function toInputTime(wire: string | null | undefined): string {
  if (!wire) return '';
  return wire.length >= 8 && wire.endsWith(':00') ? wire.slice(0, 5) : wire;
}

/** `08:00` → `08:00:00`; blank → null. */
export function toWireTime(input: string | null | undefined): string | null {
  const value = (input ?? '').trim();
  if (!value) return null;
  return value.length === 5 ? `${value}:00` : value;
}

/** `08:00:00` → `08:00` for display. */
export function formatTime(wire: string | null | undefined): string {
  return wire ? wire.slice(0, 5) : '';
}

/** Negative when `a` is earlier than `b`; accepts HH:mm and HH:mm:ss. */
export function compareTimes(a: string, b: string): number {
  const na = toWireTime(a) ?? '';
  const nb = toWireTime(b) ?? '';
  return na < nb ? -1 : na > nb ? 1 : 0;
}

// ── Internals ─────────────────────────────────────────────────────────────────

function dayOfWeekOfDayNumber(dayNumber: number): number {
  // 1970-01-01 was a Thursday (4).
  return (((dayNumber + 4) % 7) + 7) % 7;
}

function hoursOf(start: string | null | undefined, end: string | null | undefined): WorkingHours | null {
  const s = toWireTime(start);
  const e = toWireTime(end);
  return s && e ? { start: s, end: e } : null;
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}
