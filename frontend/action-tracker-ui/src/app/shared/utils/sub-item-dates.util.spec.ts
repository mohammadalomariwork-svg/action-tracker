import { isOutsideParentDates, subItemDateProblem, subItemProgressLabel, toDay } from './sub-item-dates.util';

/**
 * Repeats `ActionItemSubItemRulesTests` (server) case for case — the two copies
 * of the sub-item date rule must agree (WO-2026-11 A-2).
 */
describe('sub-item-dates.util', () => {
  const parentStart = '2030-06-03T00:00:00Z';
  const parentEnd   = '2030-06-28T00:00:00Z';
  const problem = (start: string | null, end: string, ps: string | null = parentStart, pe = parentEnd) =>
    subItemDateProblem({ start, end, parentStart: ps, parentEnd: pe });

  it('accepts dates inside the parent', () => {
    expect(problem(null, '2030-06-10')).toBeNull();
    expect(problem('2030-06-03', '2030-06-28')).toBeNull();
    expect(problem('2030-06-10', '2030-06-10')).toBeNull();
  });

  it('names the first broken rule with the server wording', () => {
    expect(problem('2030-06-12', '2030-06-11')).toBe('Sub-item end 2030-06-11 is before the sub-item start 2030-06-12.');
    expect(problem('2030-06-02', '2030-06-10')).toBe("Sub-item start 2030-06-02 is before the action item's planned start 2030-06-03.");
    expect(problem('2030-06-29', '2030-06-30')).toBe("Sub-item start 2030-06-29 is after the action item's planned end 2030-06-28.");
    expect(problem(null, '2030-06-02')).toBe("Sub-item end 2030-06-02 is before the action item's planned start 2030-06-03.");
    expect(problem('2030-06-10', '2030-06-29')).toBe("Sub-item end 2030-06-29 is after the action item's planned end 2030-06-28.");
  });

  it('only bounds the end when the parent has no planned start', () => {
    expect(problem('2020-01-06', '2030-06-28', null)).toBeNull();
    expect(problem(null, '2030-06-29', null)).toBe("Sub-item end 2030-06-29 is after the action item's planned end 2030-06-28.");
  });

  it('compares calendar days, not times', () => {
    expect(problem(null, '2030-06-28', parentStart, '2030-06-28T09:00:00Z')).toBeNull();
    expect(problem('2030-06-03', '2030-06-04', '2030-06-03T08:00:00Z')).toBeNull();
  });

  it('says nothing until the end date is there', () => {
    expect(problem('2030-06-10', '')).toBeNull();
  });

  it('flags dates outside the parent', () => {
    const outside = (start: string | null, end: string) =>
      isOutsideParentDates({ start, end, parentStart, parentEnd });
    expect(outside(null, '2030-06-10')).toBeFalse();
    expect(outside('2030-06-03', '2030-06-28')).toBeFalse();
    expect(outside('2030-06-02', '2030-06-10')).toBeTrue();
    expect(outside(null, '2030-06-29')).toBeTrue();
    expect(outside(null, '2030-06-02')).toBeTrue();
    expect(outside('2030-06-29', '2030-07-01')).toBeTrue();
  });

  it('formats the progress label', () => {
    expect(subItemProgressLabel(3, 5)).toBe('3 of 5 done');
    expect(subItemProgressLabel(0, 0)).toBe('');
    expect(subItemProgressLabel(undefined, undefined)).toBe('');
    expect(toDay('2030-06-03T00:00:00Z')).toBe('2030-06-03');
    expect(toDay('nope')).toBeNull();
  });
});
