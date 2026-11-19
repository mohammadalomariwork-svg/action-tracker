import { ActionItemFilter } from '../../../core/models/action-item.model';

/** Colour role for a badge or bar in a drill-down row. */
export type DrillTone = 'brand' | 'good' | 'warn' | 'bad' | 'neutral';

/** A pre-built row (projects, KPIs, objectives, milestones …). */
export interface DrillRow {
  /** Short code shown before the title, e.g. "PRJ-2026-004". */
  code?: string;
  title: string;
  subtitle?: string;
  badge?: string;
  badgeTone?: DrillTone;
  /** Right-aligned detail, e.g. a due date or a percentage. */
  meta?: string;
  metaTone?: DrillTone;
  /** 0–100; draws a progress bar under the title. */
  progress?: number | null;
  /** Where the row leads — its details page. */
  link: unknown[];
}

type ActionFilter = Partial<Omit<ActionItemFilter, 'pageNumber' | 'pageSize' | 'sortBy' | 'sortDescending'>>;

interface DrillBase {
  title: string;
  subtitle?: string;
}

/**
 * Action items are fetched page by page with server-side filters (there can
 * be thousands). `scope: 'mine'` lists the caller's own assignments.
 */
export interface ActionDrill extends DrillBase {
  kind: 'actions';
  scope: 'all' | 'mine';
  filter: ActionFilter;
  /** Optional link to a full page that shows the same list. */
  fullList?: { label: string; link: unknown[]; queryParams?: Record<string, string | number> };
}

/** Everything else is small enough that the dashboard already holds the rows. */
export interface RowDrill extends DrillBase {
  kind: 'rows';
  rows: DrillRow[];
  emptyText?: string;
}

export type DrilldownRequest = ActionDrill | RowDrill;
