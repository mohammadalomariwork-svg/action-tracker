/**
 * Tags on action items (WO-2026-11 A-5). A tag belongs to one workspace: it is
 * suggested there only, and a filter goes by tag id, so the same name in two
 * workspaces is two tags. The tag on an item is `TagInfo` (core action-item model).
 */

/** `GET /workspaces/:id/tags` — suggestions and the workspace list's filter options. */
export interface WorkspaceTag {
  id: string;
  name: string;
  /** Live action items the caller can see that carry the tag. */
  itemCount: number;
}

/** `GET /action-items/my-actions/tags` — My Actions filter options, labelled with their workspace (D3). */
export interface MyActionTagOption {
  id: string;
  name: string;
  workspaceId: string;
  workspaceTitle: string;
  itemCount: number;
}

/** One option of `<app-tag-filter>`. `group` labels it (My Actions: the workspace). */
export interface TagFilterOption {
  id: string;
  name: string;
  group?: string;
  count?: number;
}

export const TAG_MAX_PER_ITEM = 10;
export const TAG_MAX_LENGTH   = 30;
