/**
 * Actual start / actual end on action items (WO-2026-11 A-1, Q9) — client copy
 * of the checks in the server's `ActionItemActualDates`. **Change both together.**
 *
 *  - Actual start is set by the server on the first move into In Progress;
 *    actual end on the move into Done, and it is cleared when the item leaves Done.
 *  - Both are editable. Neither may be after today (in the workspace calendar's
 *    time zone), actual end may not be before actual start, and actual end
 *    exists only while the item is Done.
 *  - Actual dates are not frozen, not sent for approval and not checked against
 *    the working calendar.
 *
 * The server decides; this only warns before saving. Dates are `yyyy-MM-dd`.
 */

import { ActionItemUpdate, ActionStatus } from '../../core/models/action-item.model';

/** Today as `yyyy-MM-dd` in `timeZoneId` (IANA), or in UTC when it is missing or unknown. */
export function todayInTimeZone(timeZoneId?: string | null, now: Date = new Date()): string {
  try {
    if (timeZoneId) {
      // en-CA formats as yyyy-MM-dd.
      return new Intl.DateTimeFormat('en-CA', {
        timeZone: timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit',
      }).format(now);
    }
  } catch {
    // Unknown time zone: fall through to UTC, as the server does.
  }
  return now.toISOString().slice(0, 10);
}

/**
 * True when a save with `status` can set the actual end: the item is moving to
 * Done and the move applies directly. A standalone item's move to Done goes
 * for approval; its actual end is recorded when the request is approved.
 */
export function actualEndEditableFor(status: unknown, isStandalone: boolean | null | undefined): boolean {
  return Number(status) === ActionStatus.Done && !isStandalone;
}

/** First 10 characters of a wire date, or '' when there is none. */
export function toDateInput(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

export interface ActualDatesInput {
  /** Actual start in the form ('' = empty). */
  start: string;
  /** Actual end in the form ('' = empty). */
  end: string;
  /** True when the item is Done after this save (an actual end is allowed). */
  doneAfterSave: boolean;
  /** Today, from {@link todayInTimeZone}. */
  today: string;
}

/** The first problem with the actual dates, worded like the server's message, or null. */
export function actualDatesProblem(input: ActualDatesInput): string | null {
  const { start, end, doneAfterSave, today } = input;
  if (start && start > today) return `Actual start (${start}) cannot be after today (${today}).`;
  if (end && end > today) return `Actual end (${end}) cannot be after today (${today}).`;
  if (end && !doneAfterSave) return 'Actual end can only be set when the action item is Done.';
  if (start && end && end < start) return `Actual end (${end}) cannot be before actual start (${start}).`;
  return null;
}

/**
 * The PUT fields for the actual dates: only what changed. A blanked actual
 * start becomes `clearActualStartDate`; an actual end is sent only when it is
 * allowed (`endAllowed`) and changed — it cannot be cleared.
 */
export function actualDatesPatch(
  original: { start: string | null | undefined; end: string | null | undefined },
  form: { start: string; end: string },
  endAllowed: boolean,
): Pick<ActionItemUpdate, 'actualStartDate' | 'clearActualStartDate' | 'actualEndDate'> {
  const patch: Pick<ActionItemUpdate, 'actualStartDate' | 'clearActualStartDate' | 'actualEndDate'> = {};
  const oldStart = toDateInput(original.start);
  const oldEnd   = toDateInput(original.end);

  if (form.start !== oldStart) {
    if (form.start) patch.actualStartDate = form.start;
    else patch.clearActualStartDate = true;
  }
  if (endAllowed && form.end && form.end !== oldEnd) patch.actualEndDate = form.end;
  return patch;
}

/** "3 days late" / "1 day late" / "On time" / "2 days early"; null without a variance. */
export function varianceLabel(days: number | null | undefined): string | null {
  if (days === null || days === undefined) return null;
  if (days === 0) return 'On time';
  const n = Math.abs(days);
  return `${n} ${n === 1 ? 'day' : 'days'} ${days > 0 ? 'late' : 'early'}`;
}

/** Tone of a variance for styling: late, or ok (on time / early); null without a variance. */
export function varianceTone(days: number | null | undefined): 'late' | 'ok' | null {
  if (days === null || days === undefined) return null;
  return days > 0 ? 'late' : 'ok';
}
