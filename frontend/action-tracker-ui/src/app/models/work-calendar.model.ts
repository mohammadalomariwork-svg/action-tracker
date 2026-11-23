/**
 * Working calendars — Admin Panel → Calendars (`/api/calendars`, WO-2026-11 C-2).
 *
 * Wire format (set by the C-1 API):
 *  - `dayOfWeek` is a camelCase string enum: 'sunday' … 'saturday'
 *  - times are `HH:mm:ss`, wall-clock in the calendar's `timeZoneId`
 *  - dates are `yyyy-MM-dd`; an exception's range is inclusive
 */

export type WeekDayName =
  | 'sunday'
  | 'monday'
  | 'tuesday'
  | 'wednesday'
  | 'thursday'
  | 'friday'
  | 'saturday';

/** Index order matches JavaScript's `Date.getUTCDay()` and .NET's `DayOfWeek` (0 = Sunday). */
export const WEEK_DAY_NAMES: readonly WeekDayName[] = [
  'sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday',
];

/** Display order used by the page: the KU week starts on Monday. */
export const WEEK_DAYS_MONDAY_FIRST: readonly WeekDayName[] = [
  'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday',
];

export const WEEK_DAY_LABELS: Record<WeekDayName, { short: string; long: string }> = {
  sunday:    { short: 'Sun', long: 'Sunday' },
  monday:    { short: 'Mon', long: 'Monday' },
  tuesday:   { short: 'Tue', long: 'Tuesday' },
  wednesday: { short: 'Wed', long: 'Wednesday' },
  thursday:  { short: 'Thu', long: 'Thursday' },
  friday:    { short: 'Fri', long: 'Friday' },
  saturday:  { short: 'Sat', long: 'Saturday' },
};

/** One day of the weekly pattern. */
export interface WorkCalendarDay {
  dayOfWeek: WeekDayName;
  isWorkingDay: boolean;
  /** `HH:mm:ss`; null on a non-working day. */
  startTime?: string | null;
  endTime?: string | null;
}

/** A dated override of the weekly pattern (holiday, special working day, changed hours). */
export interface WorkCalendarException {
  /** Null/absent on input = a new exception; set = update that existing row. */
  id?: string | null;
  name: string;
  /** `yyyy-MM-dd`, inclusive. */
  startDate: string;
  endDate: string;
  /** false = off (holiday / closure); true = working (special working day or changed hours). */
  isWorkingDay: boolean;
  /** Optional hours for a working exception; null keeps the weekly pattern's hours. */
  startTime?: string | null;
  endTime?: string | null;
}

export interface WorkCalendar {
  id: string;
  name: string;
  description?: string | null;
  timeZoneId: string;
  isDefault: boolean;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  /** Seven rows, Sunday to Saturday. */
  days: WorkCalendarDay[];
  /** Ordered by start date. */
  exceptions: WorkCalendarException[];
}

/**
 * Body of `POST /api/calendars` and `PUT /api/calendars/{id}`.
 * `days` must hold all seven days; on update `exceptions` is the **full** list —
 * rows with an `id` are updated, rows without one are added, rows left out are removed.
 */
export interface WorkCalendarInput {
  name: string;
  description?: string | null;
  timeZoneId: string;
  isActive: boolean;
  days: WorkCalendarDay[];
  exceptions: WorkCalendarException[];
}

export interface WorkCalendarUsageWorkspace {
  id: string;
  title: string;
  isActive: boolean;
}

/** `GET /api/calendars/{id}/usage`. */
export interface WorkCalendarUsage {
  calendarId: string;
  isDefault: boolean;
  /** Every workspace naming this calendar, including ones the caller cannot see (drives the delete guard). */
  totalWorkspaceCount: number;
  /** Workspaces naming this calendar that the caller may see. */
  workspaces: WorkCalendarUsageWorkspace[];
  /** Workspaces naming this calendar that the caller cannot see (private workspaces). */
  hiddenWorkspaceCount: number;
  /** Default calendar only: visible workspaces with no calendar of their own. */
  inheritingWorkspaceCount: number;
}

export const DEFAULT_CALENDAR_TIME_ZONE = 'Asia/Dubai';

/** Time zones offered in the editor. A calendar holding another id keeps it as an extra option. */
export const CALENDAR_TIME_ZONE_OPTIONS: { id: string; label: string }[] = [
  { id: 'Asia/Dubai', label: 'Gulf Standard Time — Asia/Dubai (UTC+4)' },
  { id: 'UTC',        label: 'UTC' },
];

/** Field limits, mirroring `WorkCalendarInputValidator` on the server. */
export const CALENDAR_LIMITS = {
  nameMax: 100,
  descriptionMax: 500,
  exceptionNameMax: 150,
} as const;
