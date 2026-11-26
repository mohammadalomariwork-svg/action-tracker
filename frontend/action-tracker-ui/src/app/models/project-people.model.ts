/**
 * Project people (WO-2026-11 P-2): the lead project manager (`Project.projectManagerUserId`),
 * additional project managers, sponsors, viewers and team members of one project.
 */

/** A caller's role on a project, as `myRoles` on project responses carries it. */
export type ProjectRoleName = 'leadProjectManager' | 'projectManager' | 'sponsor' | 'viewer' | 'teamMember';

/** A role people can be added to or removed from (`POST` / `DELETE /projects/:id/people`). */
export type ProjectPersonRole = 'projectManager' | 'sponsor' | 'viewer' | 'teamMember';

/** One person on a project (`GET /projects/:id/people`). */
export interface ProjectPerson {
  userId: string;
  fullName: string;
  email: string;
  orgUnitName?: string | null;
  isActive: boolean;
}

/** `GET /projects/:id/people`: everyone linked to the project, by role. */
export interface ProjectPeople {
  projectId: string;
  leadProjectManager: ProjectPerson | null;
  /** Additional project managers (the lead is not in this list). */
  projectManagers: ProjectPerson[];
  sponsors: ProjectPerson[];
  viewers: ProjectPerson[];
  teamMembers: ProjectPerson[];
  /** The caller may add and remove people. */
  canManage: boolean;
  /** The caller may add project managers — needs the workspace's Project Managers list (D4). */
  canAddProjectManagers: boolean;
  myRoles: ProjectRoleName[];
}

/** A person in a picker: from the lookup, the PM list, assignable users or the project itself. */
export interface PersonOption {
  userId: string;
  fullName: string;
  email?: string | null;
  orgUnitName?: string | null;
}

/** `GET /projects/people-lookup` (any active user — D13). */
export interface PeopleLookupUser {
  id: string;
  displayName: string;
  email?: string | null;
  orgUnitName?: string | null;
}

/** The people block of a project edit screen. */
export interface ProjectPeopleValue {
  additionalManagers: PersonOption[];
  sponsors: PersonOption[];
  viewers: PersonOption[];
  teamMembers: PersonOption[];
}

export const PROJECT_ROLE_LABELS: Record<ProjectRoleName, string> = {
  leadProjectManager: 'Lead project manager',
  projectManager:     'Project manager',
  sponsor:            'Sponsor',
  viewer:             'Viewer',
  teamMember:         'Team member',
};

/** Order roles are listed in, strongest first. */
export const PROJECT_ROLE_ORDER: ProjectRoleName[] = [
  'leadProjectManager', 'projectManager', 'sponsor', 'viewer', 'teamMember',
];
