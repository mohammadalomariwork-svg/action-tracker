import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { By } from '@angular/platform-browser';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { ProjectPeoplePanelComponent } from './project-people-panel.component';
import { ProjectPeopleService } from '../../../../services/project-people.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { ToastService } from '../../../../core/services/toast.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ProjectPeople, ProjectPerson } from '../../../../models/project-people.model';
import { isShownHere } from '../../../../core/http/server-reason';

/** Stand-in for the Bootstrap modal (the real one needs Bootstrap's global JS). */
@Component({ selector: 'app-confirm-dialog', standalone: true, template: '' })
class StubConfirmDialogComponent {
  readonly title = input('');
  readonly message = input('');
  readonly confirmLabel = input('');
  readonly cancelLabel = input('');
  readonly danger = input(false);
  readonly confirmed = output<boolean>();
  opened = 0;
  open(): void { this.opened++; }
}

const person = (userId: string, fullName: string, isActive = true): ProjectPerson =>
  ({ userId, fullName, email: `${userId}@ku.ac.ae`, orgUnitName: 'Digital Services', isActive });

function people(over: Partial<ProjectPeople> = {}): ProjectPeople {
  return {
    projectId: 'P1',
    leadProjectManager: person('lead', 'Layla Lead'),
    projectManagers: [person('pm2', 'Pavel Second')],
    sponsors: [person('s1', 'Sara Sponsor'), person('s2', 'Samir Sponsor')],
    viewers: [person('v1', 'Vera Viewer', false)],
    teamMembers: [],
    canManage: true,
    canAddProjectManagers: true,
    myRoles: ['leadProjectManager'],
    ...over,
  };
}

// WO-2026-11 P-2: the People panel on the project page.
describe('ProjectPeoplePanelComponent (WO-2026-11 P-2)', () => {
  let fixture: ComponentFixture<ProjectPeoplePanelComponent>;
  let svc: jasmine.SpyObj<ProjectPeopleService>;
  let wsSvc: jasmine.SpyObj<WorkspaceService>;
  let toast: jasmine.SpyObj<ToastService>;
  let granted: Set<string>;
  let host: HTMLElement;

  async function render(data: ProjectPeople, opts: { perms?: string[]; workspaceId?: string | null } = {}): Promise<void> {
    granted = new Set(opts.perms ?? ['Projects.View', 'Projects.Edit']);
    svc.get.and.returnValue(of({ success: true, data } as any));
    await TestBed.configureTestingModule({
      imports: [ProjectPeoplePanelComponent],
      providers: [
        { provide: ProjectPeopleService, useValue: svc },
        { provide: WorkspaceService, useValue: wsSvc },
        { provide: ToastService, useValue: toast },
        {
          provide: PermissionStateService,
          useValue: {
            permissions$: new BehaviorSubject<unknown[]>([]),
            hasPermission: (area: string, action: string) => granted.has(`${area}.${action}`),
          },
        },
      ],
    })
      .overrideComponent(ProjectPeoplePanelComponent, {
        remove: { imports: [ConfirmDialogComponent] },
        add: { imports: [StubConfirmDialogComponent] },
      })
      .compileComponents();
    fixture = TestBed.createComponent(ProjectPeoplePanelComponent);
    fixture.componentRef.setInput('projectId', 'P1');
    fixture.componentRef.setInput('workspaceId', opts.workspaceId === undefined ? 'W1' : opts.workspaceId);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    host = fixture.nativeElement;
  }

  const group = (id: string) => host.querySelector(`[aria-labelledby="ppl-${id}"]`) as HTMLElement | null;
  const removeBtn = (label: string) => host.querySelector(`button[aria-label="${label}"]`) as HTMLButtonElement | null;
  const roleOptions = () => [...host.querySelectorAll('#ppl-add-role option')].map(o => (o as HTMLOptionElement).value);

  beforeEach(() => {
    svc = jasmine.createSpyObj<ProjectPeopleService>('ProjectPeopleService', ['get', 'add', 'remove', 'lookup']);
    svc.lookup.and.returnValue(of([]));
    wsSvc = jasmine.createSpyObj<WorkspaceService>('WorkspaceService', ['getProjectManagers']);
    wsSvc.getProjectManagers.and.returnValue(of({ success: true, data: [
      { userId: 'pm3', userName: 'Pia Third', email: 'pm3@ku.ac.ae', orgUnitName: 'DS', addedAt: '' },
    ] } as any));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info', 'warning']);
  });

  it('lists every role; the lead PM is not removable here; an inactive person is marked', async () => {
    await render(people());
    expect(group('leadProjectManager')!.textContent).toContain('Layla Lead');
    expect(group('projectManager')!.textContent).toContain('Pavel Second');
    expect(group('viewer')!.textContent).toContain('inactive');
    expect(host.textContent).toContain('No team members.');
    expect(host.textContent).toContain('Your role: Lead project manager');
    expect(removeBtn('Remove Layla Lead as lead project manager')).toBeNull();
    expect(removeBtn('Remove Pavel Second as project manager')).not.toBeNull();
    expect(roleOptions()).toEqual(['projectManager', 'sponsor', 'viewer', 'teamMember']);
    expect(wsSvc.getProjectManagers).toHaveBeenCalledWith('W1');
  });

  it('removes a person after the confirmation and focuses the heading again', async () => {
    await render(people());
    svc.remove.and.returnValue(of({ success: true, data: people({ viewers: [] }) } as any));
    removeBtn('Remove Vera Viewer as viewer')!.click();
    fixture.detectChanges();

    const dialog = fixture.debugElement.query(By.directive(StubConfirmDialogComponent))
      .componentInstance as StubConfirmDialogComponent;
    expect(dialog.opened).toBe(1);
    expect(svc.remove).not.toHaveBeenCalled();

    dialog.confirmed.emit(true);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(svc.remove).toHaveBeenCalledWith('P1', 'viewer', 'v1');
    expect(host.textContent).toContain('No viewers.');
    expect(toast.success).toHaveBeenCalledWith('Vera Viewer removed as viewer.');
    expect(document.activeElement?.id).toBe('ppl-heading');
  });

  it('cancelling the confirmation removes nobody', async () => {
    await render(people());
    removeBtn('Remove Pavel Second as project manager')!.click();
    fixture.detectChanges();
    const dialog = fixture.debugElement.query(By.directive(StubConfirmDialogComponent))
      .componentInstance as StubConfirmDialogComponent;
    dialog.confirmed.emit(false);
    expect(svc.remove).not.toHaveBeenCalled();
  });

  it('the last sponsor cannot be removed', async () => {
    await render(people({ sponsors: [person('s1', 'Sara Sponsor')] }));
    expect(removeBtn('Remove Sara Sponsor as sponsor')!.disabled).toBeTrue();
    expect(host.textContent).toContain('A project needs at least one sponsor');
  });

  it('shows the server reason for a failed removal on the panel, not as a second toast', async () => {
    await render(people());
    const err = new HttpErrorResponse({ status: 403, error: { message: 'Only the project managers can change who is on the project.' } });
    svc.remove.and.returnValue(throwError(() => err));
    removeBtn('Remove Vera Viewer as viewer')!.click();
    fixture.detectChanges();
    const dialog = fixture.debugElement.query(By.directive(StubConfirmDialogComponent))
      .componentInstance as StubConfirmDialogComponent;
    dialog.confirmed.emit(true);
    fixture.detectChanges();
    expect(host.querySelector('[role="alert"]')!.textContent).toContain('Only the project managers can change who is on the project.');
    expect(isShownHere(err)).toBeTrue();
  });

  it('adds a person in the chosen role and reports a refusal on the form', async () => {
    await render(people());
    const panel = fixture.componentInstance;
    panel.onRoleChange('teamMember');
    panel.addPerson.set([{ userId: 't1', fullName: 'Tariq Team' }]);
    svc.add.and.returnValue(of({ success: true, data: people({ teamMembers: [person('t1', 'Tariq Team')] }) } as any));
    fixture.detectChanges();

    (host.querySelector('form.ppl__add button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(svc.add).toHaveBeenCalledWith('P1', 't1', 'teamMember');
    expect(group('teamMember')!.textContent).toContain('Tariq Team');

    const conflict = new HttpErrorResponse({ status: 409, error: { message: 'This person already has that role on the project.' } });
    svc.add.and.returnValue(throwError(() => conflict));
    panel.addPerson.set([{ userId: 't1', fullName: 'Tariq Team' }]);
    fixture.detectChanges();
    (host.querySelector('form.ppl__add button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(host.querySelector('form.ppl__add [role="alert"]')!.textContent).toContain('already has that role');
    expect(isShownHere(conflict)).toBeTrue();
  });

  it('a project-only PM (D4) is not offered project managers and the PM list is never requested', async () => {
    await render(people({ canAddProjectManagers: false, myRoles: ['projectManager'] }), { workspaceId: null });
    expect(roleOptions()).toEqual(['sponsor', 'viewer', 'teamMember']);
    expect(wsSvc.getProjectManagers).not.toHaveBeenCalled();
    expect(host.textContent).toContain("Only the workspace's admins and members can add project managers.");
    expect(removeBtn('Remove Pavel Second as project manager')).not.toBeNull();   // removing is allowed
  });

  it('a viewer sees the people but no add form and no remove buttons', async () => {
    await render(people({ canManage: false, canAddProjectManagers: false, myRoles: ['viewer'] }));
    expect(host.querySelector('form.ppl__add')).toBeNull();
    expect(host.querySelectorAll('.ppl__remove').length).toBe(0);
    expect(host.textContent).toContain('Your role: Viewer');
    expect(wsSvc.getProjectManagers).not.toHaveBeenCalled();
  });

  it('without Projects.Edit there is nothing to change even for a manager', async () => {
    await render(people(), { perms: ['Projects.View'] });
    expect(host.querySelector('form.ppl__add')).toBeNull();
    expect(host.querySelectorAll('.ppl__remove').length).toBe(0);
  });

  it('a load failure shows the reason with a retry, not a toast', async () => {
    const err = new HttpErrorResponse({ status: 404, error: { message: 'Project P1 not found.' } });
    svc.get.and.returnValue(throwError(() => err));
    granted = new Set(['Projects.View']);
    await TestBed.configureTestingModule({
      imports: [ProjectPeoplePanelComponent],
      providers: [
        { provide: ProjectPeopleService, useValue: svc },
        { provide: WorkspaceService, useValue: wsSvc },
        { provide: ToastService, useValue: toast },
        { provide: PermissionStateService, useValue: { permissions$: new BehaviorSubject([]), hasPermission: () => true } },
      ],
    }).overrideComponent(ProjectPeoplePanelComponent, {
      remove: { imports: [ConfirmDialogComponent] }, add: { imports: [StubConfirmDialogComponent] },
    }).compileComponents();
    fixture = TestBed.createComponent(ProjectPeoplePanelComponent);
    fixture.componentRef.setInput('projectId', 'P1');
    fixture.detectChanges();
    host = fixture.nativeElement;
    expect(host.querySelector('[role="alert"]')!.textContent).toContain('Project P1 not found.');
    expect(isShownHere(err)).toBeTrue();
    expect(host.textContent).toContain('Try again');
  });
});
