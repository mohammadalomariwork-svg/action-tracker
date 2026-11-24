/**
 * Sub-item date rule (WO-2026-11 A-2) — the client copy of the server's
 * `ActionItemSubItemRules` (`Application/Features/ActionItemSubItems`).
 * **Change both together**; `sub-item-dates.util.spec.ts` repeats
 * `ActionItemSubItemRulesTests`. Same wording as the server's 400.
 *
 * A sub-item's dates must lie within its parent's planned dates: end not
 * before start; neither date after the parent's planned end; neither date
 * before the parent's planned start when it has one. Compared as calendar
 * days (`yyyy-MM-dd`), so the browser's time zone never shifts a date.
 * The working-day rule is separate (`working-day.validator.ts`).
 */

export interface SubItemDates {
  /** `yyyy-MM-dd`, or empty / null for no start. */
  start: string | null | undefined;
  /** `yyyy-MM-dd`. */
  end: string | null | undefined;
  /** The parent's planned start (any ISO string; null = none). */
  parentStart: string | null | undefined;
  /** The parent's planned end (any ISO string). */
  parentEnd: string | null | undefined;
}

/** `yyyy-MM-dd` from a date input value or an ISO timestamp; null otherwise. */
export function toDay(value: string | null | undefined): string | null {
  if (!value) return null;
  const day = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(day) ? day : null;
}

/**
 * The first broken rule, or null when the dates are fine. Says nothing while
 * the end or the parent's planned end is missing (the required check covers it).
 */
export function subItemDateProblem(d: SubItemDates): string | null {
  const s  = toDay(d.start);
  const e  = toDay(d.end);
  const ps = toDay(d.parentStart);
  const pe = toDay(d.parentEnd);
  if (!e || !pe) return null;

  if (s && e < s)            return `Sub-item end ${e} is before the sub-item start ${s}.`;
  if (s && ps && s < ps)     return `Sub-item start ${s} is before the action item's planned start ${ps}.`;
  if (s && s > pe)           return `Sub-item start ${s} is after the action item's planned end ${pe}.`;
  if (ps && e < ps)          return `Sub-item end ${e} is before the action item's planned start ${ps}.`;
  if (e > pe)                return `Sub-item end ${e} is after the action item's planned end ${pe}.`;
  return null;
}

/** True when the dates lie outside the parent's planned dates (the server's `IsOutsideParentDates`). */
export function isOutsideParentDates(d: SubItemDates): boolean {
  const s  = toDay(d.start);
  const e  = toDay(d.end);
  const ps = toDay(d.parentStart);
  const pe = toDay(d.parentEnd);
  if (!e || !pe) return false;
  if (e > pe || (s !== null && s > pe)) return true;
  if (ps && (e < ps || (s !== null && s < ps))) return true;
  return false;
}

/** "3 of 5 done" (empty for an item without sub-items). */
export function subItemProgressLabel(done: number | null | undefined, total: number | null | undefined): string {
  return total ? `${done ?? 0} of ${total} done` : '';
}
