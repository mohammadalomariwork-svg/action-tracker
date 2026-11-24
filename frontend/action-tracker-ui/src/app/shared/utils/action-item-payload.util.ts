/**
 * Edit bodies for PUT /api/action-items/{id} (WO-2026-11 A-1b).
 *
 * The edit drawers build one payload for create and edit. Before A-1b that
 * payload carried `isStandalone: true` on every edit, which turned a
 * project-linked item into a standalone one. The flag now follows the project
 * link on the server; an edit body never carries it.
 */

import { ActionItemCreate, ActionItemUpdate } from '../../core/models/action-item.model';

/** `payload` without `isStandalone`, for an edit. Every other field is kept. */
export function toActionItemUpdate(payload: ActionItemCreate | ActionItemUpdate): ActionItemUpdate {
  const { isStandalone: _ignored, ...update } = payload as ActionItemCreate;
  return update;
}
