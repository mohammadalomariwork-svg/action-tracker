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
 * Same cases as `ResolvedWorkCalendarTests` on the server (the functions the
 * preview uses: working day, hours, working days between). If one of these
 * fails, the Calendars page preview no longer matches `IWorkCalendarService`.
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
