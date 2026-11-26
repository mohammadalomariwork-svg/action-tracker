import {
  emptyPeopleValue, isProjectManagerOf, peoplePatch, peopleValueFrom, primaryProjectRole, projectRolesLabel, sameUserIds,
} from './project-people.util';
import { ProjectPeople, ProjectPeopleValue } from '../../models/project-people.model';

const p = (userId: string) => ({ userId, fullName: userId.toUpperCase() });

describe('project-people.util (WO-2026-11 P-2)', () => {
  const original: ProjectPeopleValue = {
    additionalManagers: [p('pm2')],
    sponsors: [p('s1')],
    viewers: [p('v1'), p('v2')],
    teamMembers: [],
  };

  it('always sends sponsors and leaves unchanged lists out, so the server keeps them', () => {
    const payload = peoplePatch(original, structuredClone(original));
    expect(payload).toEqual({ sponsorUserIds: ['s1'] });
  });

  it('sends only the lists that changed, also when emptied', () => {
    const current: ProjectPeopleValue = {
      ...structuredClone(original),
      viewers: [p('v2'), p('v1')],          // same people, other order: unchanged
      teamMembers: [p('t1')],
      additionalManagers: [],
    };
    expect(peoplePatch(original, current)).toEqual({
      sponsorUserIds: ['s1'],
      additionalManagerUserIds: [],
      teamMemberUserIds: ['t1'],
    });
  });

  it('on create sends a list only when someone is in it', () => {
    const current = { ...emptyPeopleValue([p('s1')]), viewers: [p('v1')] };
    expect(peoplePatch(null, current)).toEqual({ sponsorUserIds: ['s1'], viewerUserIds: ['v1'] });
  });

  it('compares ids without regard to case or order', () => {
    expect(sameUserIds([p('AbC'), p('x')], [p('X'), p('abc')])).toBeTrue();
    expect(sameUserIds([p('a')], [p('a'), p('b')])).toBeFalse();
  });

  it('builds the value from GET /people', () => {
    const people: ProjectPeople = {
      projectId: 'P', leadProjectManager: { userId: 'lead', fullName: 'Lead', email: 'l@x', isActive: true },
      projectManagers: [{ userId: 'pm2', fullName: 'PM Two', email: 'p@x', isActive: true }],
      sponsors: [{ userId: 's1', fullName: 'S', email: 's@x', orgUnitName: 'DS', isActive: true }],
      viewers: [], teamMembers: [], canManage: true, canAddProjectManagers: false, myRoles: ['projectManager'],
    };
    const value = peopleValueFrom(people);
    expect(value.additionalManagers.map(x => x.userId)).toEqual(['pm2']);
    expect(value.sponsors[0]).toEqual({ userId: 's1', fullName: 'S', email: 's@x', orgUnitName: 'DS' });
  });

  it('lead and additional PMs may submit (D3); other roles not', () => {
    expect(isProjectManagerOf(['leadProjectManager'])).toBeTrue();
    expect(isProjectManagerOf(['sponsor', 'projectManager'])).toBeTrue();
    expect(isProjectManagerOf(['sponsor', 'viewer', 'teamMember'])).toBeFalse();
    expect(isProjectManagerOf(undefined)).toBeFalse();
  });

  it('names the roles strongest first', () => {
    expect(projectRolesLabel(['teamMember', 'sponsor'])).toBe('Sponsor, Team member');
    expect(primaryProjectRole(['viewer', 'projectManager'])).toBe('projectManager');
    expect(primaryProjectRole([])).toBeNull();
  });
});
