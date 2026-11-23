import { FormControl } from '@angular/forms';
import type { WorkCalendarDay, WorkCalendarException, WorkspaceCalendar } from '../../models/work-calendar.model';
import { toWorkspaceCalendarRules } from '../../services/workspace-calendar.service';
import {
  checkWorkingDay,
  findNonWorkingDates,
  nonWorkingDatesMessage,
  toIsoDateOrNull,
  workingDayCount,
  workingDayValidator,
} from './working-day.validator';

/**
 * Client side of the WO-2026-11 C-3 working-day rule. The messages must match
 * `NonWorkingDateException` on the server (see `WorkingDateRuleTests`).
 * Reference week: Monday 23 Nov 2026 … Sunday 29 Nov 2026.
 */
describe('working-day.validator', () => {
  const NAMES = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'] as const;

  function week(...working: string[]): WorkCalendarDay[] {
    return NAMES.map(name => ({
      dayOfWeek: name,
      isWorkingDay: working.includes(name),
      startTime: working.includes(name) ? '08:00:00' : null,
      endTime: working.includes(name) ? '17:00:00' : null,
    }));
  }

  function calendar(
    days = week('monday', 'tuesday', 'wednesday', 'thursday', 'friday'),
    exceptions: WorkCalendarException[] = [],
    name = 'KU Standard',
  ) {
    const wire: WorkspaceCalendar = {
      workspaceId: 'ws-1',
      workCalendarId: null,
      calendarId: 'cal-1',
      calendarName: name,
      timeZoneId: 'Asia/Dubai',
      isDefault: true,
      isInherited: true,
      from: '2026-01-01',
      to: '2027-12-31',
      days,
      exceptions,
    };
    return toWorkspaceCalendarRules(wire);
  }

  it('refuses a Saturday with the same wording as the server', () => {
    const problem = checkWorkingDay(calendar(), '2026-11-28', null, 'Due date');

    expect(problem).not.toBeNull();
    expect(problem!.message).toBe(
      'Due date 2026-11-28 (Saturday) is not a working day in the workspace calendar "KU Standard". ' +
      'Next working day: 2026-11-30 (Monday).');
    expect(problem!.hint).toBe('Saturday 28 Nov 2026 is not a working day in KU Standard.');
    expect(problem!.nextWorkingDay).toBe('2026-11-30');
    expect(problem!.nextWorkingDayLabel).toBe('Mon 30 Nov 2026');
  });

  it('accepts a working day', () => {
    expect(checkWorkingDay(calendar(), '2026-11-27')).toBeNull();
  });

  it('never flags an unchanged date (Q8), even on a weekend', () => {
    expect(checkWorkingDay(calendar(), '2026-11-28', '2026-11-28')).toBeNull();
    expect(checkWorkingDay(calendar(), '2026-11-28', '2026-11-28T00:00:00Z')).toBeNull();
    expect(checkWorkingDay(calendar(), '2026-11-29', '2026-11-28')).not.toBeNull();
  });

  it('names the holiday and skips it for the next working day', () => {
    const cal = calendar(undefined, [
      { name: 'National Day', startDate: '2026-12-02', endDate: '2026-12-03', isWorkingDay: false },
    ]);

    const problem = checkWorkingDay(cal, '2026-12-02', null, 'Planned end date');

    expect(problem!.hint).toBe('Wednesday 2 Dec 2026 is a holiday in KU Standard (National Day).');
    expect(problem!.nextWorkingDay).toBe('2026-12-04');
  });

  it('follows the workspace calendar: a six-day week accepts Saturday', () => {
    const sixDay = calendar(week('monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'), [], 'Six-day week');

    expect(checkWorkingDay(sixDay, '2026-11-28')).toBeNull();
    expect(checkWorkingDay(sixDay, '2026-11-29')!.hint).toContain('Six-day week');
  });

  it('says nothing when it cannot judge: no calendar, outside the window, not a date', () => {
    expect(checkWorkingDay(null, '2026-11-28')).toBeNull();
    expect(checkWorkingDay(calendar(), '2030-06-01')).toBeNull(); // Saturday, but outside the loaded window
    expect(checkWorkingDay(calendar(), '')).toBeNull();
    expect(checkWorkingDay(calendar(), 'not-a-date')).toBeNull();
  });

  it('lists every refused field and joins the messages like the server', () => {
    const fields = [
      { field: 'Start date', value: '2026-11-28' },
      { field: 'Due date',   value: '2026-11-29' },
      { field: 'Other',      value: '2026-11-30' },
    ];

    expect(findNonWorkingDates(calendar(), fields).map(p => p.field)).toEqual(['Start date', 'Due date']);
    expect(nonWorkingDatesMessage(calendar(), fields)).toBe(
      'Start date 2026-11-28 (Saturday) is not a working day in the workspace calendar "KU Standard". ' +
      'Next working day: 2026-11-30 (Monday). ' +
      'Due date 2026-11-29 (Sunday) is not a working day in the workspace calendar "KU Standard". ' +
      'Next working day: 2026-11-30 (Monday).');
    expect(nonWorkingDatesMessage(calendar(), [{ field: 'Due date', value: '2026-11-30' }])).toBeNull();
  });

  it('counts the duration in working days, both ends included', () => {
    const cal = calendar();

    expect(workingDayCount(cal, '2026-11-23', '2026-11-29')).toBe(5);
    expect(workingDayCount(cal, '2026-11-28', '2026-11-29')).toBe(0);
    expect(workingDayCount(cal, '2026-11-27', '2026-11-23')).toBeNull(); // end before start
    expect(workingDayCount(cal, '', '2026-11-27')).toBeNull();
    expect(workingDayCount(null, '2026-11-23', '2026-11-27')).toBeNull();
  });

  it('works as a reactive-forms validator that follows the calendar and the original value', () => {
    let cal: ReturnType<typeof calendar> | null = null;
    let original: string | null = null;
    const control = new FormControl('2026-11-28', workingDayValidator(() => cal, () => original, 'Due date'));

    expect(control.valid).withContext('no calendar yet: the server decides').toBeTrue();

    cal = calendar();
    control.updateValueAndValidity();
    expect(control.hasError('nonWorkingDay')).toBeTrue();
    expect(control.getError('nonWorkingDay').nextWorkingDay).toBe('2026-11-30');

    original = '2026-11-28';
    control.updateValueAndValidity();
    expect(control.valid).withContext('legacy weekend date left unchanged').toBeTrue();

    control.setValue('2026-11-30');
    expect(control.valid).toBeTrue();
  });

  it('reads yyyy-MM-dd out of date inputs and ISO timestamps only', () => {
    expect(toIsoDateOrNull('2026-11-28')).toBe('2026-11-28');
    expect(toIsoDateOrNull('2026-11-28T09:30:00Z')).toBe('2026-11-28');
    expect(toIsoDateOrNull('28/11/2026')).toBeNull();
    expect(toIsoDateOrNull(null)).toBeNull();
  });
});
