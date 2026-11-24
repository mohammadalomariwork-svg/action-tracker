import { ActionStatus } from '../../core/models/action-item.model';
import {
  actualDatesPatch,
  actualDatesProblem,
  actualEndEditableFor,
  todayInTimeZone,
  toDateInput,
  varianceLabel,
  varianceTone,
} from './actual-dates.util';

// Mirrors ActionItemActualDatesTests (backend) for the checks the client repeats.
describe('actual-dates.util (WO-2026-11 A-1)', () => {
  describe('todayInTimeZone', () => {
    // 21:30 UTC on 24 Nov is 01:30 on 25 Nov in Dubai (UTC+4).
    const now = new Date(Date.UTC(2026, 10, 24, 21, 30));

    it('uses the calendar time zone', () => {
      expect(todayInTimeZone('Asia/Dubai', now)).toBe('2026-11-25');
    });

    it('falls back to the UTC day', () => {
      expect(todayInTimeZone(null, now)).toBe('2026-11-24');
      expect(todayInTimeZone('', now)).toBe('2026-11-24');
      expect(todayInTimeZone('Not/AZone', now)).toBe('2026-11-24');
    });
  });

  it('actualEndEditableFor allows an actual end only on a direct move to Done', () => {
    expect(actualEndEditableFor(ActionStatus.Done, false)).toBeTrue();
    expect(actualEndEditableFor('4', false)).toBeTrue();
    expect(actualEndEditableFor(ActionStatus.Done, true)).toBeFalse();   // standalone: goes for approval
    expect(actualEndEditableFor(ActionStatus.InProgress, false)).toBeFalse();
  });

  it('toDateInput keeps the day of a wire date', () => {
    expect(toDateInput('2026-11-20T00:00:00Z')).toBe('2026-11-20');
    expect(toDateInput(null)).toBe('');
  });

  describe('actualDatesProblem', () => {
    const today = '2026-11-24';

    it('accepts valid values and empty fields', () => {
      expect(actualDatesProblem({ start: '', end: '', doneAfterSave: false, today })).toBeNull();
      expect(actualDatesProblem({ start: '2026-11-20', end: '2026-11-24', doneAfterSave: true, today })).toBeNull();
      expect(actualDatesProblem({ start: '2026-11-20', end: '2026-11-20', doneAfterSave: true, today })).toBeNull();
    });

    it('refuses a date after today', () => {
      expect(actualDatesProblem({ start: '2026-11-25', end: '', doneAfterSave: false, today }))
        .toBe('Actual start (2026-11-25) cannot be after today (2026-11-24).');
      expect(actualDatesProblem({ start: '', end: '2026-11-25', doneAfterSave: true, today }))
        .toBe('Actual end (2026-11-25) cannot be after today (2026-11-24).');
    });

    it('refuses an actual end unless the item is Done', () => {
      expect(actualDatesProblem({ start: '', end: '2026-11-20', doneAfterSave: false, today }))
        .toBe('Actual end can only be set when the action item is Done.');
    });

    it('refuses an actual end before the actual start', () => {
      expect(actualDatesProblem({ start: '2026-11-21', end: '2026-11-20', doneAfterSave: true, today }))
        .toBe('Actual end (2026-11-20) cannot be before actual start (2026-11-21).');
    });
  });

  describe('actualDatesPatch', () => {
    it('sends nothing when nothing changed', () => {
      expect(actualDatesPatch({ start: '2026-11-20T00:00:00Z', end: null }, { start: '2026-11-20', end: '' }, false))
        .toEqual({});
    });

    it('sends a changed start, and a cleared start as clearActualStartDate', () => {
      expect(actualDatesPatch({ start: null, end: null }, { start: '2026-11-19', end: '' }, false))
        .toEqual({ actualStartDate: '2026-11-19' });
      expect(actualDatesPatch({ start: '2026-11-19', end: null }, { start: '', end: '' }, false))
        .toEqual({ clearActualStartDate: true });
    });

    it('sends a changed end only when allowed, and never clears it', () => {
      expect(actualDatesPatch({ start: null, end: '2026-11-24' }, { start: '', end: '2026-11-22' }, true))
        .toEqual({ actualEndDate: '2026-11-22' });
      expect(actualDatesPatch({ start: null, end: null }, { start: '', end: '2026-11-22' }, false)).toEqual({});
      expect(actualDatesPatch({ start: null, end: '2026-11-24' }, { start: '', end: '' }, true)).toEqual({});
    });
  });

  it('varianceLabel and varianceTone describe actual end minus planned end', () => {
    expect(varianceLabel(3)).toBe('3 days late');
    expect(varianceLabel(1)).toBe('1 day late');
    expect(varianceLabel(0)).toBe('On time');
    expect(varianceLabel(-2)).toBe('2 days early');
    expect(varianceLabel(null)).toBeNull();
    expect(varianceTone(3)).toBe('late');
    expect(varianceTone(0)).toBe('ok');
    expect(varianceTone(-1)).toBe('ok');
    expect(varianceTone(null)).toBeNull();
  });
});
