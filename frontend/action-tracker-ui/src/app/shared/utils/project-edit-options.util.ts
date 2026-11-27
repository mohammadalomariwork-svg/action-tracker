import { ProjectStatus } from '../../features/projects/models/project.models';

/**
 * WO-2026-11 P-2b — options of the four project edit screens (project form page,
 * project page drawer, My Projects drawers, workspace page drawer).
 *
 * Status (fix 1): a plain edit offers only the statuses the server's
 * `ProjectStatusRules` allows from the current one — the response's
 * `allowedStatuses`; leaving Draft other than to Cancelled goes through
 * Submit for Approval. No client copy of the rule: the server's list is used.
 *
 * Lead PM (fix 6): the dropdown lists the workspace's Project Managers list; a
 * current lead who is not on it stays selectable, marked as such, so a save
 * that keeps them is still accepted (the server checks the list only on a change).
 */

export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  [ProjectStatus.Draft]: 'Draft',
  [ProjectStatus.PendingApproval]: 'Pending Approval',
  [ProjectStatus.Active]: 'Active',
  [ProjectStatus.OnHold]: 'On Hold',
  [ProjectStatus.Completed]: 'Completed',
  [ProjectStatus.Cancelled]: 'Cancelled',
};

const STATUS_ORDER: readonly ProjectStatus[] = [
  ProjectStatus.Draft, ProjectStatus.PendingApproval, ProjectStatus.Active,
  ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled,
];

export interface ProjectStatusOption {
  value: ProjectStatus;
  label: string;
}

/**
 * The statuses a plain edit may set, in display order. Without `allowed` (an
 * older response) only the current status is offered — the save then cannot
 * be refused for its status.
 */
export function editableStatusOptions(
  current: ProjectStatus, allowed: readonly ProjectStatus[] | null | undefined,
): ProjectStatusOption[] {
  const set = new Set<ProjectStatus>(allowed && allowed.length ? allowed : []);
  set.add(current);
  return STATUS_ORDER.filter(s => set.has(s)).map(s => ({ value: s, label: PROJECT_STATUS_LABELS[s] }));
}

export const SUBMIT_FOR_APPROVAL_HINT =
  'A Draft project becomes Active through Submit for Approval on the project page; here it can only stay in Draft or be cancelled.';

export const PENDING_APPROVAL_HINT =
  'The status changes through the approval workflow while the project is pending approval.';

/** A short note under the status field, or null. */
export function statusEditHint(current: ProjectStatus): string | null {
  if (current === ProjectStatus.Draft) return SUBMIT_FOR_APPROVAL_HINT;
  if (current === ProjectStatus.PendingApproval) return PENDING_APPROVAL_HINT;
  return null;
}

export interface LeadPmOption {
  userId: string;
  label: string;
  /** The current lead, not on the workspace's Project Managers list. */
  offList: boolean;
}

export const NOT_ON_PM_LIST = 'not on the Project Managers list';

/**
 * Lead-PM options: the workspace's Project Managers list by name, plus the current
 * lead when they are not on it (labelled), first. `pmList` null = not loaded yet.
 */
export function leadPmOptions(
  pmList: readonly { userId: string; fullName: string }[] | null | undefined,
  currentLeadUserId: string | null | undefined,
  currentLeadName: string | null | undefined,
): LeadPmOption[] {
  const list = (pmList ?? [])
    .map(p => ({ userId: p.userId, label: p.fullName || p.userId, offList: false }))
    .sort((a, b) => a.label.localeCompare(b.label));
  const lead = (currentLeadUserId ?? '').trim();
  if (lead && !list.some(o => o.userId.toLowerCase() === lead.toLowerCase())) {
    const name = (currentLeadName ?? '').trim() || lead;
    list.unshift({ userId: lead, label: `${name} (${NOT_ON_PM_LIST})`, offList: true });
  }
  return list;
}
