import {
  ChangeDetectionStrategy, Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, input,
  output, signal, viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProjectPeopleService } from '../../../../services/project-people.service';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { ToastService } from '../../../../core/services/toast.service';
import { showErrorHere } from '../../../../core/http/server-reason';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PersonPickerComponent } from '../../../../shared/components/person-picker/person-picker.component';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import {
  PROJECT_ROLE_LABELS, PersonOption, ProjectPeople, ProjectPerson, ProjectPersonRole,
} from '../../../../models/project-people.model';
import { projectRolesLabel, userListOptions } from '../../../../shared/utils/project-people.util';

interface PeopleGroup {
  role: ProjectPersonRole | 'leadProjectManager';
  title: string;
  people: ProjectPerson[];
  empty: string;
}

/**
 * The People panel of the project page (WO-2026-11 P-2): the lead PM,
 * additional PMs, sponsors, viewers and team members, with add and remove for
 * callers who manage the project (`canManage` from `GET /projects/:id/people`,
 * plus `Projects.Edit`). Adding a project manager needs the workspace's
 * Project Managers list, so it is offered only with `canAddProjectManagers`
 * (D4). The lead PM is changed through Edit. Removing asks first; the access
 * through that role ends on the person's next request.
 */
@Component({
  selector: 'app-project-people-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ConfirmDialogComponent, PersonPickerComponent, HasPermissionDirective],
  templateUrl: './project-people-panel.component.html',
  styleUrl: './project-people-panel.component.scss',
})
export class ProjectPeoplePanelComponent implements OnInit {
  private readonly peopleSvc    = inject(ProjectPeopleService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly toast        = inject(ToastService);
  private readonly injector     = inject(Injector);

  readonly projectId   = input.required<string>();
  readonly workspaceId = input<string | null>(null);
  /** Emitted after an add or remove, so the page can reload the project. */
  readonly changed = output<void>();

  readonly people    = signal<ProjectPeople | null>(null);
  readonly loading   = signal(true);
  readonly loadError = signal<string | null>(null);

  // Add form
  readonly addRole   = signal<ProjectPersonRole>('teamMember');
  readonly addPerson = signal<PersonOption[]>([]);
  readonly adding    = signal(false);
  readonly addError  = signal<string | null>(null);
  readonly pmOptions = signal<PersonOption[] | null>(null);

  // Remove
  readonly removeError = signal<string | null>(null);
  readonly removing    = signal<string | null>(null);
  pendingRemove: { role: ProjectPersonRole; person: ProjectPerson } | null = null;
  confirmMessage = '';

  private readonly confirm = viewChild<ConfirmDialogComponent>('confirmRemove');
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  readonly lookup = (term: string) => this.peopleSvc.lookup(term);

  readonly canManage = computed(() => !!this.people()?.canManage);
  readonly myRolesLabel = computed(() => projectRolesLabel(this.people()?.myRoles));

  readonly roleChoices = computed(() => {
    const p = this.people();
    const list: { value: ProjectPersonRole; label: string }[] = [];
    if (p?.canAddProjectManagers) list.push({ value: 'projectManager', label: 'Project manager' });
    list.push(
      { value: 'sponsor', label: 'Sponsor' },
      { value: 'viewer', label: 'Viewer' },
      { value: 'teamMember', label: 'Team member' },
    );
    return list;
  });

  readonly groups = computed<PeopleGroup[]>(() => {
    const p = this.people();
    if (!p) return [];
    return [
      { role: 'leadProjectManager', title: 'Lead project manager', people: p.leadProjectManager ? [p.leadProjectManager] : [], empty: 'None.' },
      { role: 'projectManager', title: 'Project managers', people: p.projectManagers, empty: 'No additional project managers.' },
      { role: 'sponsor', title: 'Sponsors', people: p.sponsors, empty: 'No sponsors.' },
      { role: 'viewer', title: 'Viewers', people: p.viewers, empty: 'No viewers.' },
      { role: 'teamMember', title: 'Team members', people: p.teamMembers, empty: 'No team members.' },
    ];
  });

  /** People already in the chosen role — not offered again. */
  readonly takenForRole = computed(() => {
    const p = this.people();
    if (!p) return [];
    switch (this.addRole()) {
      case 'projectManager': return [...p.projectManagers.map(x => x.userId), ...(p.leadProjectManager ? [p.leadProjectManager.userId] : [])];
      case 'sponsor':        return p.sponsors.map(x => x.userId);
      case 'viewer':         return p.viewers.map(x => x.userId);
      case 'teamMember':     return p.teamMembers.map(x => x.userId);
    }
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.peopleSvc.get(this.projectId()).subscribe({
      next: res => {
        this.people.set(res.data ?? null);
        this.loading.set(false);
        this.loadPmOptions();
      },
      error: err => {
        this.loadError.set(showErrorHere(err) ?? 'Could not load the project\'s people.');
        this.loading.set(false);
      },
    });
  }

  /** The workspace's Project Managers list — only for callers who see the workspace (D4). */
  private loadPmOptions(): void {
    const ws = this.workspaceId();
    if (!this.people()?.canAddProjectManagers || !ws || this.pmOptions() !== null) return;
    this.workspaceSvc.getProjectManagers(ws).subscribe({
      next: res => this.pmOptions.set(userListOptions(res.data)),
      error: err => { showErrorHere(err); this.pmOptions.set([]); },
    });
  }

  onRoleChange(role: ProjectPersonRole): void {
    this.addRole.set(role);
    this.addPerson.set([]);
    this.addError.set(null);
  }

  add(): void {
    const person = this.addPerson()[0];
    if (!person || this.adding()) return;
    const role = this.addRole();
    this.adding.set(true);
    this.addError.set(null);
    this.peopleSvc.add(this.projectId(), person.userId, role).subscribe({
      next: res => {
        this.people.set(res.data ?? this.people());
        this.adding.set(false);
        this.addPerson.set([]);
        this.toast.success(`${person.fullName} added as ${PROJECT_ROLE_LABELS[role].toLowerCase()}.`);
        this.changed.emit();
      },
      error: err => {
        this.addError.set(showErrorHere(err) ?? 'Could not add this person.');
        this.adding.set(false);
      },
    });
  }

  askRemove(role: ProjectPersonRole, person: ProjectPerson): void {
    this.removeError.set(null);
    this.pendingRemove = { role, person };
    this.confirmMessage =
      `Remove ${person.fullName} as ${PROJECT_ROLE_LABELS[role].toLowerCase()}? ` +
      'Their access to this project through this role ends at once.';
    this.confirm()?.open();
  }

  onConfirmRemove(ok: boolean): void {
    const pending = this.pendingRemove;
    this.pendingRemove = null;
    if (!ok || !pending) { this.focusHeading(); return; }
    const key = `${pending.role}:${pending.person.userId}`;
    this.removing.set(key);
    this.peopleSvc.remove(this.projectId(), pending.role, pending.person.userId).subscribe({
      next: res => {
        this.people.set(res.data ?? this.people());
        this.removing.set(null);
        this.toast.success(`${pending.person.fullName} removed as ${PROJECT_ROLE_LABELS[pending.role].toLowerCase()}.`);
        this.changed.emit();
        this.focusHeading();
      },
      error: err => {
        this.removeError.set(showErrorHere(err) ?? 'Could not remove this person.');
        this.removing.set(null);
        this.focusHeading();
      },
    });
  }

  roleLabel(role: string): string {
    return (PROJECT_ROLE_LABELS[role as keyof typeof PROJECT_ROLE_LABELS] ?? role).toLowerCase();
  }

  isRemoving(role: string, userId: string): boolean {
    return this.removing() === `${role}:${userId}`;
  }

  /** The last sponsor cannot be removed (the server refuses it too). */
  isLastSponsor(role: string): boolean {
    return role === 'sponsor' && (this.people()?.sponsors.length ?? 0) <= 1;
  }

  private focusHeading(): void {
    // The confirm dialog does not restore focus; the panel's heading takes it.
    afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
  }
}
