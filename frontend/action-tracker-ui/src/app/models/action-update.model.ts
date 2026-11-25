/**
 * Action Updates (WO-2026-11 A-3) — they replace comments on action items and
 * sub-items: `/api/action-items/{id}/updates` and
 * `/api/action-items/{id}/sub-items/{subItemId}/updates`.
 *
 * A draft is visible to its author only and can be edited, submitted or
 * deleted by them. Submit is one-way: a submitted update is read-only for
 * every role (the API answers 409). Updates never count as action items.
 */
export type ActionUpdateStatus = 'draft' | 'submitted';

export interface ActionUpdate {
  id: string;
  actionItemId: string;
  /** Null for an update on the action item itself. */
  subItemId: string | null;
  content: string;
  authorUserId: string;
  authorName: string;
  isHighImportance: boolean;
  status: ActionUpdateStatus;
  isDraft: boolean;
  /** Null while it is a draft. */
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** True only for the caller's own draft. */
  canEdit: boolean;
  /** Files attached (WO-2026-11 A-4) — the paperclip count. */
  documentCount: number;
}

/** Every submitted update plus the caller's own drafts (drafts first). */
export interface ActionUpdateList {
  actionItemId: string;
  subItemId: string | null;
  submittedCount: number;
  draftCount: number;
  items: ActionUpdate[];
}

export interface ActionUpdateCount {
  submittedCount: number;
  draftCount: number;
}

export interface SubItemActionUpdateCount extends ActionUpdateCount {
  subItemId: string;
}

/** Counts for the action item and for each sub-item that has updates the caller can see. */
export interface ActionUpdateCounts {
  actionItemId: string;
  item: ActionUpdateCount;
  subItems: SubItemActionUpdateCount[];
}

export interface ActionUpdateInput {
  content: string;
  isHighImportance: boolean;
}

/** New update: a draft, or submitted straight away when `submit` is true. */
export interface ActionUpdateCreate extends ActionUpdateInput {
  submit: boolean;
}

/** Files per update (WO-2026-11 A-4, Q13). */
export const ACTION_UPDATE_MAX_FILES = 10;

/** Same limit as the API (after trimming). */
export const ACTION_UPDATE_MAX_LENGTH = 2000;
