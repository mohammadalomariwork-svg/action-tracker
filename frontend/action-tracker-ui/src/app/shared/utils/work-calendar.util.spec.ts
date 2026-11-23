import type { WorkCalendarDay, WorkCalendarException } from '../../models/work-calendar.model';
import {
  addDays,
  compareTimes,
  createCalendarRules,
  dayOfWeekOf,
  toInputTime,
  toWireTime,
} from './work-calendar.util';

/**
 * Same cases as `ResolvedWorkCalendarTests` on the server (working day, hours,
 * working days between, next / previous working day, adding working days, and
 * the tie-break between overlapping exceptions). If one of these fails, the
 * Calendars page preview and the date-field warnings no longer match
 * `IWorkCalendarService`.
 * Reference week: Monday 23 Nov 2026 … Sunday 29 Nov 2026.
 */
describe('work-calendar.util', () => {
  const NAMES = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'] as const;

  /** Monday–Friday 08:00–17:00, Saturday–Sunday off. */
  function standardWeek(): WorkCalendarDay[] {
    return NAMES.map(name => {
      const working = name !== 'saturday' && name !== 'sunday';
      return {
        dayOfWeek: name,
        isWorkingDay: working,
        startTime: working ? '08:00:00' : null,
        endTime: working ? '17:00:00' : null,
      };
    });
  }

  function standard(...exceptions: WorkCalendarException[]) {
    return createCalendarRules(standardWeek(), exceptions);
  }

  function holiday(name: string, startDate: string, endDate: string): WorkCalendarException {
    return { name, startDate, endDate, isWorkingDay: false };
  }

  // ── Weekend ────────────────────────────────────────────────────────────────

  it('follows the weekly pattern', () => {
    const calendar = standard();

    expect(dayOfWeekOf('2026-11-27')).toBe(5); // Friday
    expect(calendar.isWorkingDay('2026-11-27')).toBeTrue();
    expect(calendar.isWorkingDay('2026-11-28')).toBeFalse(); // Saturday
    expect(calendar.isWorkingDay('2026-11-29')).toBeFalse(); // Sunday
    expect(calendar.isWorkingDay('2026-11-30')).toBeTrue();
    expect(calendar.resolve('2026-11-28').kind).toBe('off');
    expect(calendar.getWorkingHours('2026-11-27')).toEqual({ start: '08:00:00', end: '17:00:00' });
    expect(calendar.getWorkingHours('2026-11-28')).toBeNull();
  });

  it('counts working days with both ends included and ignores the weekend', () => {
    const calendar = standard();

    expect(calendar.workingDaysBetween('2026-11-23', '2026-11-29')).toBe(5); // Mon..Sun
    expect(calendar.workingDaysBetween('2026-11-23', '2026-11-23')).toBe(1);
    expect(calendar.workingDaysBetween('2026-11-28', '2026-11-29')).toBe(0); // weekend only
    expect(calendar.workingDaysBetween('2026-11-27', '2026-11-23')).toBe(0); // end before start
  });

  // ── Holiday range ──────────────────────────────────────────────────────────

  it('lets a holiday range override the weekly pattern on every day it covers', () => {
    const calendar = standard(holiday('National Day', '2026-12-02', '2026-12-03'));

    expect(calendar.isWorkingDay('2026-12-01')).toBeTrue();
    expect(calendar.isWorkingDay('2026-12-02')).toBeFalse();
    expect(calendar.isWorkingDay('2026-12-03')).toBeFalse();
    expect(calendar.isWorkingDay('2026-12-04')).toBeTrue();
    expect(calendar.workingDaysBetween('2026-11-30', '2026-12-04')).toBe(3); // Mon, Tue, Fri
    expect(calendar.getWorkingHours('2026-12-02')).toBeNull();

    const day = calendar.resolve('2026-12-02');
    expect(day.kind).toBe('holiday');
    expect(day.exception?.name).toBe('National Day');
    expect(day.weeklyIsWorkingDay).toBeTrue();
  });

  // ── Special working day ────────────────────────────────────────────────────

  it('turns a weekend day into a working day with its own hours', () => {
    expect(dayOfWeekOf('2026-12-05')).toBe(6); // Saturday

    const calendar = standard({
      name: 'Make-up Saturday',
      startDate: '2026-12-05',
      endDate: '2026-12-05',
      isWorkingDay: true,
      startTime: '09:00:00',
      endTime: '13:00:00',
    });

    expect(calendar.isWorkingDay('2026-12-05')).toBeTrue();
    expect(calendar.isWorkingDay('2026-12-12')).toBeFalse(); // only that one Saturday
    expect(calendar.workingDaysBetween('2026-11-30', '2026-12-06')).toBe(6);
    expect(calendar.getWorkingHours('2026-12-05')).toEqual({ start: '09:00:00', end: '13:00:00' });
    expect(calendar.resolve('2026-12-05').kind).toBe('special');
  });

  // ── Year boundary ──────────────────────────────────────────────────────────

  it('counts across the year boundary', () => {
    expect(dayOfWeekOf('2026-12-31')).toBe(4); // Thursday

    const calendar = standard(holiday('New Year', '2027-01-01', '2027-01-01'));

    expect(calendar.isWorkingDay('2027-01-01')).toBeFalse();
    expect(calendar.workingDaysBetween('2026-12-28', '2027-01-08')).toBe(9); // 10 weekdays − 1 holiday
  });

  it('honours a holiday range spanning the year boundary on both sides', () => {
    const calendar = standard(holiday('Winter closure', '2026-12-30', '2027-01-05'));

    expect(calendar.isWorkingDay('2026-12-29')).toBeTrue();
    expect(calendar.isWorkingDay('2026-12-31')).toBeFalse();
    expect(calendar.isWorkingDay('2027-01-05')).toBeFalse();
    expect(calendar.isWorkingDay('2027-01-06')).toBeTrue();
  });

  // ── Overlapping exceptions / reduced hours ─────────────────────────────────

  it('lets the shortest overlapping exception win, whatever the order', () => {
    const ramadan: WorkCalendarException = {
      name: 'Ramadan hours',
      startDate: '2027-02-08',
      endDate: '2027-03-09',
      isWorkingDay: true,
      startTime: '09:00:00',
      endTime: '14:00:00',
    };
    const inside = holiday('Holiday inside Ramadan', '2027-02-16', '2027-02-17');

    for (const calendar of [standard(ramadan, inside), standard(inside, ramadan)]) {
      expect(calendar.isWorkingDay('2027-02-15')).toBeTrue();
      expect(calendar.getWorkingHours('2027-02-15')).toEqual({ start: '09:00:00', end: '14:00:00' });
      expect(calendar.isWorkingDay('2027-02-16')).toBeFalse();
      expect(calendar.isWorkingDay('2027-02-17')).toBeFalse();
      expect(calendar.isWorkingDay('2027-02-18')).toBeTrue();
    }
  });

  it('lets a non-working exception win a tie of equal length', () => {
    const working: WorkCalendarException = {
      name: 'Open day', startDate: '2026-12-10', endDate: '2026-12-10', isWorkingDay: true,
    };
    const off = holiday('Closed', '2026-12-10', '2026-12-10');

    expect(standard(working, off).isWorkingDay('2026-12-10')).toBeFalse();
    expect(standard(off, working).isWorkingDay('2026-12-10')).toBeFalse();
  });

  // Two working exceptions of the same length on the same date — same cases as
  // the `TiedWorkingExceptions_*` tests on the server.

  function working(name: string, startDate: string, endDate: string, startTime?: string, endTime?: string): WorkCalendarException {
    return { name, startDate, endDate, isWorkingDay: true, startTime: startTime ?? null, endTime: endTime ?? null };
  }

  it('lets the shorter working day win a tie of equal length, whatever the order', () => {
    const examHours = working('Exam week hours', '2026-12-07', '2026-12-11', '08:00:00', '15:00:00');
    const shortDay  = working('Short week', '2026-12-07', '2026-12-11', '09:00:00', '13:00:00');

    for (const calendar of [standard(examHours, shortDay), standard(shortDay, examHours)]) {
      expect(calendar.getWorkingHours('2026-12-09')).toEqual({ start: '09:00:00', end: '13:00:00' });
      expect(calendar.resolve('2026-12-09').exception?.name).toBe('Short week');
    }
  });

  it('lets the earlier start win a tie of equal-length hours, whatever the order', () => {
    const morning   = working('Morning shift', '2026-12-10', '2026-12-10', '08:00:00', '12:00:00');
    const afternoon = working('Afternoon shift', '2026-12-10', '2026-12-10', '09:00:00', '13:00:00');

    for (const calendar of [standard(morning, afternoon), standard(afternoon, morning)]) {
      expect(calendar.getWorkingHours('2026-12-10')).toEqual({ start: '08:00:00', end: '12:00:00' });
    }
  });

  it('lets a working exception with hours of its own beat one without, whatever the order', () => {
    const marked  = working('Marked day', '2026-12-10', '2026-12-10');
    const reduced = working('Reduced hours', '2026-12-10', '2026-12-10', '10:00:00', '14:00:00');

    for (const calendar of [standard(marked, reduced), standard(reduced, marked)]) {
      expect(calendar.getWorkingHours('2026-12-10')).toEqual({ start: '10:00:00', end: '14:00:00' });
    }
  });

  it('settles identical exceptions by name (ordinal), then start date, whatever the order', () => {
    const b = holiday('Bank holiday', '2026-12-10', '2026-12-10');
    const a = holiday('Annual closure', '2026-12-10', '2026-12-10');
    for (const calendar of [standard(a, b), standard(b, a)]) {
      expect(calendar.resolve('2026-12-10').exception?.name).toBe('Annual closure');
    }

    // Same name and length, overlapping on 2–3 Dec: the earlier start decides both days.
    const first  = holiday('Closure', '2026-12-01', '2026-12-03');
    const second = holiday('Closure', '2026-12-02', '2026-12-04');
    for (const calendar of [standard(first, second), standard(second, first)]) {
      expect(calendar.resolve('2026-12-02').exception?.startDate).toBe('2026-12-01');
    }
  });

  // ── Working-day search ─────────────────────────────────────────────────────

  it('finds the next and previous working day, keeping a working day as it is', () => {
    const calendar = standard(holiday('National Day', '2026-11-30', '2026-12-01'));

    expect(calendar.nextWorkingDay('2026-11-28')).toBe('2026-12-02'); // Sat → past Mon–Tue holiday → Wed
    expect(calendar.nextWorkingDay('2026-11-25')).toBe('2026-11-25');
    expect(calendar.previousWorkingDay('2026-11-29')).toBe('2026-11-27'); // Sun → Fri
    expect(calendar.previousWorkingDay('2026-11-27')).toBe('2026-11-27');
  });

  it('adds working days like the server (start not counted, negative goes back)', () => {
    const calendar = standard();

    expect(calendar.addWorkingDays('2026-11-27', 1)).toBe('2026-11-30'); // Fri + 1 → Mon
    expect(calendar.addWorkingDays('2026-11-23', 5)).toBe('2026-11-30');
    expect(calendar.addWorkingDays('2026-11-30', -1)).toBe('2026-11-27');
    expect(calendar.addWorkingDays('2026-11-28', 0)).toBe('2026-11-28');
  });

  it('returns null instead of looping when no working day is reachable', () => {
    const closed = createCalendarRules(standardWeek().map(d => ({ ...d, isWorkingDay: false })), []);

    expect(closed.nextWorkingDay('2026-11-23')).toBeNull();
    expect(closed.addWorkingDays('2026-11-23', 1)).toBeNull();
  });

  it('keeps the weekly hours on a working exception without hours', () => {
    const calendar = standard({
      name: 'Marked week', startDate: '2026-11-23', endDate: '2026-11-27', isWorkingDay: true,
    });

    expect(calendar.getWorkingHours('2026-11-24')).toEqual({ start: '08:00:00', end: '17:00:00' });
  });

  // ── Draft values the editor can hold ───────────────────────────────────────

  it('treats a missing day row as non-working and ignores hours left on an off day', () => {
    const week = standardWeek().filter(d => d.dayOfWeek !== 'wednesday');
    week[0] = { dayOfWeek: 'sunday', isWorkingDay: false, startTime: '08:00:00', endTime: '17:00:00' };
    const calendar = createCalendarRules(week, []);

    expect(calendar.isWorkingDay('2026-11-25')).toBeFalse(); // Wednesday has no row
    expect(calendar.getWorkingHours('2026-11-29')).toBeNull(); // Sunday off despite stray times
  });

  it('ignores an exception whose dates are incomplete', () => {
    const calendar = standard({ name: 'Half typed', startDate: '2026-11-24', endDate: '', isWorkingDay: false });

    expect(calendar.isWorkingDay('2026-11-24')).toBeTrue();
  });

  // ── Helpers ────────────────────────────────────────────────────────────────

  it('converts between wire and input times', () => {
    expect(toInputTime('08:00:00')).toBe('08:00');
    expect(toInputTime('08:30:15')).toBe('08:30:15');
    expect(toInputTime(null)).toBe('');
    expect(toWireTime('08:00')).toBe('08:00:00');
    expect(toWireTime('08:30:15')).toBe('08:30:15');
    expect(toWireTime('')).toBeNull();
    expect(compareTimes('08:00', '17:00:00')).toBeLessThan(0);
    expect(compareTimes('17:00', '17:00:00')).toBe(0);
  });

  it('walks dates in whole days across month and year ends', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2027-03-01', -1)).toBe('2027-02-28');
    expect(addDays('2028-03-01', -1)).toBe('2028-02-29');
  });
});
