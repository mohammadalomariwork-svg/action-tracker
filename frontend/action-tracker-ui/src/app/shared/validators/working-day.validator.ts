/**
 * Working-day rule for date fields (WO-2026-11 C-3).
 *
 * The client side of `IWorkCalendarService.EnsureWorkingDaysAsync`:
 *  - Q7: a start or end date may not fall on a non-working day of the
 *    workspace's calendar;
 *  - Q8: only a date that is created or changed is checked — a date equal to the
 *    value the record already holds (`original`) always passes, so existing
 *    records keep saving.
 *
 * The client never refuses a date it cannot judge: with no calendar loaded, or a
 * date outside the loaded window, every function here says "fine" and the
 * server decides. The messages match the server's wording.
 *
 * Reactive forms attach {@link workingDayValidator} to their date controls;
 * template-driven forms call {@link nonWorkingDatesMessage} before saving.
 * Everything here is a pure function (Angular is imported for types only).
 */
import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { WEEK_DAY_LABELS, WEEK_DAY_NAMES } from '../../models/work-calendar.model';
import type { WorkspaceCalendarRules } from '../../services/workspace-calendar.service';
import { dayOfWeekOf, isIsoDate } from '../utils/work-calendar.util';

const MONTHS_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export interface NonWorkingDayProblem {
  /** Field label, e.g. "Due date". */
  field: string;
  /** The refused date, `yyyy-MM-dd`. */
  date: string;
  /** First working day after it, `yyyy-MM-dd`; null when none is reachable. */
  nextWorkingDay: string | null;
  /** Server wording, for a toast on save: "Due date 2026-11-28 (Saturday) is not a working day in …". */
  message: string;
  /** Short wording for under the field: "Saturday 28 Nov 2026 is not a working day in KU Standard." */
  hint: string;
  /** Button text for the fix: "Mon 30 Nov 2026". */
  nextWorkingDayLabel: string | null;
}

/** One date field of a form, for {@link findNonWorkingDates}. */
export interface WorkingDateField {
  field: string;
  value: unknown;
  /** The value the record holds now; leave out on create. */
  original?: unknown;
}

/** `yyyy-MM-dd` from a date input value or an ISO timestamp; null for anything else. */
export function toIsoDateOrNull(value: unknown): string | null {
  if (typeof value !== 'string') return null;
  const day = value.slice(0, 10);
  return isIsoDate(day) ? day : null;
}

/** The problem with one date field, or null when it may be saved. */
export function checkWorkingDay(
  calendar: WorkspaceCalendarRules | null | undefined,
  value: unknown,
  original?: unknown,
  field = 'Date',
): NonWorkingDayProblem | null {
  const date = toIsoDateOrNull(value);
  if (!date || !calendar || !calendar.covers(date)) return null;

  // Q8: an unchanged date is never refused.
  if (date === toIsoDateOrNull(original)) return null;

  const day = calendar.rules.resolve(date);
  if (day.isWorkingDay) return null;

  const next = calendar.rules.nextWorkingDay(date);
  const name = calendar.calendar.calendarName;

  const serverSentence = `${field} ${date} (${longDayName(date)}) is not a working day in the workspace calendar "${name}".`;
  const hint = day.kind === 'holiday' && day.exception?.name
    ? `${longDate(date)} is a holiday in ${name} (${day.exception.name}).`
    : `${longDate(date)} is not a working day in ${name}.`;

  return {
    field,
    date,
    nextWorkingDay: next,
    message: next ? `${serverSentence} Next working day: ${next} (${longDayName(next)}).` : serverSentence,
    hint,
    nextWorkingDayLabel: next ? shortDate(next) : null,
  };
}

/**
 * Reactive-forms validator: `{ nonWorkingDay: NonWorkingDayProblem }` when the
 * control holds a changed date that is not a working day. Reads the calendar
 * and the original value on every run, so call `updateValueAndValidity()` on
 * the control when either changes (e.g. when the calendar finishes loading).
 */
export function workingDayValidator(
  calendar: () => WorkspaceCalendarRules | null | undefined,
  original: () => unknown = () => null,
  field = 'Date',
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const problem = checkWorkingDay(calendar(), control.value, original(), field);
    return problem ? { nonWorkingDay: problem } : null;
  };
}

/** Every refused field, in the order given. */
export function findNonWorkingDates(
  calendar: WorkspaceCalendarRules | null | undefined,
  fields: readonly WorkingDateField[],
): NonWorkingDayProblem[] {
  return fields
    .map(f => checkWorkingDay(calendar, f.value, f.original, f.field))
    .filter((p): p is NonWorkingDayProblem => p !== null);
}

/** The text a save shows when it is refused, or null when every date may be saved. */
export function nonWorkingDatesMessage(
  calendar: WorkspaceCalendarRules | null | undefined,
  fields: readonly WorkingDateField[],
): string | null {
  const problems = findNonWorkingDates(calendar, fields);
  return problems.length ? problems.map(p => p.message).join(' ') : null;
}

/**
 * Working days from `start` to `end`, both counted; null when the count cannot
 * be given (a date missing, end before start, or outside the loaded window).
 */
export function workingDayCount(
  calendar: WorkspaceCalendarRules | null | undefined,
  start: unknown,
  end: unknown,
): number | null {
  const from = toIsoDateOrNull(start);
  const to   = toIsoDateOrNull(end);
  if (!calendar || !from || !to || to < from) return null;
  if (!calendar.covers(from) || !calendar.covers(to)) return null;
  return calendar.rules.workingDaysBetween(from, to);
}

// ── Formatting ────────────────────────────────────────────────────────────────

function longDayName(iso: string): string {
  return WEEK_DAY_LABELS[WEEK_DAY_NAMES[dayOfWeekOf(iso)]].long;
}

/** "Saturday 28 Nov 2026" */
function longDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return `${longDayName(iso)} ${d} ${MONTHS_SHORT[m - 1]} ${y}`;
}

/** "Mon 30 Nov 2026" */
function shortDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return `${WEEK_DAY_LABELS[WEEK_DAY_NAMES[dayOfWeekOf(iso)]].short} ${d} ${MONTHS_SHORT[m - 1]} ${y}`;
}
