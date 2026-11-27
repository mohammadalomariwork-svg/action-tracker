import { ProjectStatus } from '../../features/projects/models/project.models';
import {
  editableStatusOptions, leadPmOptions, NOT_ON_PM_LIST, statusEditHint, SUBMIT_FOR_APPROVAL_HINT,
} from './project-edit-options.util';

describe('project-edit-options.util (WO-2026-11 P-2b)', () => {
  it('offers only the allowed statuses, in display order, the current one included', () => {
    expect(editableStatusOptions(ProjectStatus.Draft, [ProjectStatus.Cancelled, ProjectStatus.Draft]).map(o => o.value))
      .toEqual([ProjectStatus.Draft, ProjectStatus.Cancelled]);
    expect(editableStatusOptions(ProjectStatus.Active,
      [ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled]).map(o => o.label))
      .toEqual(['Active', 'On Hold', 'Completed', 'Cancelled']);
  });

  it('falls back to the current status when the response has no list', () => {
    expect(editableStatusOptions(ProjectStatus.OnHold, undefined).map(o => o.value)).toEqual([ProjectStatus.OnHold]);
    expect(editableStatusOptions(ProjectStatus.Completed, []).map(o => o.value)).toEqual([ProjectStatus.Completed]);
  });

  it('points a Draft project to Submit for Approval', () => {
    expect(statusEditHint(ProjectStatus.Draft)).toBe(SUBMIT_FOR_APPROVAL_HINT);
    expect(statusEditHint(ProjectStatus.PendingApproval)).toContain('approval workflow');
    expect(statusEditHint(ProjectStatus.Active)).toBeNull();
  });

  it('lists the Project Managers list by name and keeps a current lead who is not on it, marked', () => {
    const list = [{ userId: 'u-b', fullName: 'Bea' }, { userId: 'u-a', fullName: 'Ali' }];
    expect(leadPmOptions(list, 'u-a', 'Ali').map(o => o.label)).toEqual(['Ali', 'Bea']);

    const kept = leadPmOptions(list, 'u-x', 'Xena');
    expect(kept[0]).toEqual({ userId: 'u-x', label: `Xena (${NOT_ON_PM_LIST})`, offList: true });
    expect(kept.slice(1).map(o => o.userId)).toEqual(['u-a', 'u-b']);

    expect(leadPmOptions(null, 'U-A', 'Ali')[0].offList).toBeTrue();   // list not loaded yet
    expect(leadPmOptions(list, 'U-A', 'Ali').some(o => o.offList)).toBeFalse();   // ids compare without case
    expect(leadPmOptions(list, '', null).length).toBe(2);
  });
});
