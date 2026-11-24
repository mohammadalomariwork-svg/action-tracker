import { ActionItemCreate, ActionPriority, ActionStatus } from '../../core/models/action-item.model';
import { toActionItemUpdate } from './action-item-payload.util';

// WO-2026-11 A-1b: an edit body never carries isStandalone (it used to detach
// project-linked items). Mirrors ActionItemStandaloneFlagTests (backend).
describe('action-item-payload.util (WO-2026-11 A-1b)', () => {
  // The payload the My Actions / workspace detail / action form drawers build.
  const formPayload: ActionItemCreate = {
    title: 'Item',
    description: 'Description',
    workspaceId: 'ws-1',
    isStandalone: true,
    assigneeIds: ['user-1'],
    priority: ActionPriority.Medium,
    status: ActionStatus.ToDo,
    startDate: null,
    dueDate: '2030-06-03',
    progress: 0,
    isEscalated: false,
  };

  it('drops isStandalone: true', () => {
    expect('isStandalone' in toActionItemUpdate(formPayload)).toBeFalse();
  });

  it('drops isStandalone: false (milestone detail)', () => {
    const update = toActionItemUpdate({ ...formPayload, isStandalone: false, projectId: 'p-1', milestoneId: 'm-1' });
    expect('isStandalone' in update).toBeFalse();
    expect(update.projectId).toBe('p-1');
    expect(update.milestoneId).toBe('m-1');
  });

  it('keeps every other field', () => {
    const { isStandalone: _dropped, ...rest } = formPayload;
    expect(toActionItemUpdate(formPayload)).toEqual(rest);
  });

  it('does not change the payload it is given', () => {
    const payload = { ...formPayload };
    toActionItemUpdate(payload);
    expect(payload.isStandalone).toBeTrue();
  });

  it('survives JSON, so the PUT body has no isStandalone', () => {
    expect(JSON.stringify(toActionItemUpdate(formPayload))).not.toContain('isStandalone');
  });
});
