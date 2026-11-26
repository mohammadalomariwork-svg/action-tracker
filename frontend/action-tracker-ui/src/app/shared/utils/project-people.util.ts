import {
  PROJECT_ROLE_LABELS, PROJECT_ROLE_ORDER, PersonOption, ProjectPeople, ProjectPeopleValue, ProjectPerson, ProjectRoleName,
} from '../../models/project-people.model';

/**
 * Helpers for the project people block (WO-2026-11 P-2). The server decides
 * every rule (PM list, active users, last sponsor, D4); these only shape what
 * the screens send and show.
 */

export function toPersonOption(p: ProjectPerson): PersonOption {
  return { userId: p.userId, fullName: p.fullName, email: p.email, orgUnitName: p.orgUnitName ?? null };
}

/** The people block's value for a loaded project. */
export function peopleValueFrom(people: ProjectPeople): ProjectPeopleValue {
  return {
    additionalManagers: people.projectManagers.map(toPersonOption),
    sponsors:           people.sponsors.map(toPersonOption),
    viewers:            people.viewers.map(toPersonOption),
    teamMembers:        people.teamMembers.map(toPersonOption),
  };
}

export function emptyPeopleValue(sponsors: PersonOption[] = []): ProjectPeopleValue {
  return { additionalManagers: [], sponsors: [...sponsors], viewers: [], teamMembers: [] };
}

/** Same people, whatever the order or the case of the ids. */
export function sameUserIds(a: readonly { userId: string }[], b: readonly { userId: string }[]): boolean {
  const left  = new Set(a.map(p => p.userId.toLowerCase()));
  const right = new Set(b.map(p => p.userId.toLowerCase()));
  return left.size === right.size && [...left].every(id => right.has(id));
}

export interface ProjectPeoplePayload {
  sponsorUserIds: string[];
  additionalManagerUserIds?: string[];
  viewerUserIds?: string[];
  teamMemberUserIds?: string[];
}

/**
 * The people fields of a project POST / PUT. Sponsors are always sent (the
 * server needs the full list). The three other lists only when they changed:
 * on PUT a missing list keeps what the project has, so a save never undoes a
 * change made meanwhile on the People panel unless this form changed that list.
 * Without `original` (create, or the people could not be loaded) a list is sent
 * only when it has someone in it.
 */
export function peoplePatch(original: ProjectPeopleValue | null, current: ProjectPeopleValue): ProjectPeoplePayload {
  const ids = (list: PersonOption[]) => list.map(p => p.userId);
  const changed = (before: PersonOption[] | undefined, after: PersonOption[]) =>
    before ? !sameUserIds(before, after) : after.length > 0;

  const payload: ProjectPeoplePayload = { sponsorUserIds: ids(current.sponsors) };
  if (changed(original?.additionalManagers, current.additionalManagers))
    payload.additionalManagerUserIds = ids(current.additionalManagers);
  if (changed(original?.viewers, current.viewers)) payload.viewerUserIds = ids(current.viewers);
  if (changed(original?.teamMembers, current.teamMembers)) payload.teamMemberUserIds = ids(current.teamMembers);
  return payload;
}

/** Lead or additional project manager — who may press Submit (D3). */
export function isProjectManagerOf(myRoles: readonly string[] | null | undefined): boolean {
  return !!myRoles?.some(r => r === 'leadProjectManager' || r === 'projectManager');
}

/** The caller's roles, strongest first, as labels ("Lead project manager, Sponsor"). */
export function projectRolesLabel(myRoles: readonly string[] | null | undefined): string {
  return PROJECT_ROLE_ORDER
    .filter(r => myRoles?.includes(r))
    .map(r => PROJECT_ROLE_LABELS[r as ProjectRoleName])
    .join(', ');
}

/** The strongest of the caller's roles (sorting, the role column), or null. */
export function primaryProjectRole(myRoles: readonly string[] | null | undefined): ProjectRoleName | null {
  return PROJECT_ROLE_ORDER.find(r => myRoles?.includes(r)) ?? null;
}

/** The workspace's Project Managers list (or assignable users) as picker options. */
export function userListOptions(
  list: readonly { userId: string; userName: string; email?: string | null; orgUnitName?: string | null }[] | null | undefined,
): PersonOption[] {
  return (list ?? []).map(u => ({ userId: u.userId, fullName: u.userName, email: u.email ?? null, orgUnitName: u.orgUnitName ?? null }));
}
