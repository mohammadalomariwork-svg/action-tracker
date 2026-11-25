/**
 * Sub-items on action items (WO-2026-11 A-2) — `/api/action-items/{id}/sub-items`.
 * One level only. Sub-items are never counted as action items; the parent
 * shows "x of y done" and its progress is not calculated from them.
 * Dates are `yyyy-MM-dd` calendar days (the API sends midnight timestamps).
 */
export interface SubItemAssignee {
  userId: string;
  fullName: string;
  email: string;
}

export interface ActionItemSubItem {
  id: string;
  actionItemId: string;
  title: string;
  description: string | null;
  startDate: string | null;
  endDate: string;
  /** 1-based, dense. */
  sortOrder: number;
  isDone: boolean;
  doneAt: string | null;
  doneByUserId: string | null;
  doneByName: string | null;
  assignees: SubItemAssignee[];
  /** The parent's planned dates changed and no longer contain this sub-item's dates. */
  isOutsideParentDates: boolean;
  /** Files attached (WO-2026-11 A-4) — the paperclip count. */
  documentCount: number;
  createdByUserId: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/** GET response: the sub-items plus what the form needs to know about the parent. */
export interface ActionItemSubItemList {
  actionItemId: string;
  /** Planned start of the parent (null = none). */
  parentStartDate: string | null;
  /** Planned end of the parent. */
  parentDueDate: string;
  /** False when the parent is Done or Cancelled: nothing may be added or changed (409). */
  parentAcceptsChanges: boolean;
  totalCount: number;
  doneCount: number;
  items: ActionItemSubItem[];
}

/** POST / PUT body. PUT replaces every field, the assignee list included. */
export interface ActionItemSubItemInput {
  title: string;
  description: string | null;
  /** `yyyy-MM-dd` or null for no start. */
  startDate: string | null;
  /** `yyyy-MM-dd`, required. */
  endDate: string;
  assigneeIds: string[];
}
