import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';

import { PersonPickerComponent } from '../person-picker/person-picker.component';
import { ProjectPeopleService } from '../../../services/project-people.service';
import { PersonOption, ProjectPeopleValue } from '../../../models/project-people.model';

/**
 * `<app-project-people-fields [(value)]="people" ... />` — the "People" block
 * of the project edit screens (WO-2026-11 P-2): additional project managers,
 * sponsors (at least one), viewers and team members as four chip pickers.
 *
 * - Additional PMs come from `pmOptions` (the workspace's Project Managers
 *   list, like the lead PM). With `canAddProjectManagers` false — a caller who
 *   reaches the project only through a project role (D4) — nobody can be
 *   added; the chips can still be removed.
 * - Sponsors, viewers and team members: any active user, searched with
 *   `GET /projects/people-lookup` (D13). Sponsors no longer depend on the
 *   workspace, so changing the workspace keeps them.
 *
 * The form sends the result with `peoplePatch` (`shared/utils/project-people.util.ts`).
 */
@Component({
  selector: 'app-project-people-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PersonPickerComponent],
  template: `
    <fieldset class="ppf" [attr.aria-describedby]="idPrefix() + '-people-intro'">
      <legend class="ppf__legend">People</legend>
      <p class="ppf__intro" [id]="idPrefix() + '-people-intro'">
        The lead project manager is chosen above. People below see this project and everything under it, even when they are not part of the workspace.
      </p>
      <div class="ppf__grid">
        <app-person-picker
          [inputId]="idPrefix() + '-additional-pms'" label="Additional project managers"
          [options]="canAddProjectManagers() ? (pmOptions() ?? []) : []"
          [exclude]="leadExclude()" [disabled]="disabled()"
          [hint]="canAddProjectManagers()
            ? 'Same rights on this project as the lead. Only people on the workspace\\'s Project Managers list. Approval requests still go to the lead.'
            : 'Only the workspace\\'s admins and members can add project managers. You can remove them.'"
          [selected]="value().additionalManagers" (selectedChange)="patch({ additionalManagers: $event })" />

        <app-person-picker
          [inputId]="idPrefix() + '-sponsors'" label="Sponsors" [required]="true" [showErrors]="showErrors()" requiredMessage="At least one sponsor is required."
          [search]="lookup" [disabled]="disabled()"
          hint="Any active user. Sponsors can see the project; they do not edit it."
          [selected]="value().sponsors" (selectedChange)="patch({ sponsors: $event })" />

        <app-person-picker
          [inputId]="idPrefix() + '-viewers'" label="Viewers"
          [search]="lookup" [disabled]="disabled()"
          hint="Any active user. Read-only access to this project."
          [selected]="value().viewers" (selectedChange)="patch({ viewers: $event })" />

        <app-person-picker
          [inputId]="idPrefix() + '-team'" label="Team members"
          [search]="lookup" [disabled]="disabled()"
          hint="Any active user. They can be assigned this project's action items and work on the ones assigned to them."
          [selected]="value().teamMembers" (selectedChange)="patch({ teamMembers: $event })" />
      </div>
    </fieldset>
  `,
  styles: [`
    :host { display: block; }
    .ppf { margin: 0; padding: 0; border: 0; min-width: 0; }
    .ppf__legend { float: none; width: auto; margin: 0 0 .25rem; font-size: .875rem; font-weight: 700; color: var(--t1); }
    .ppf__intro { margin: 0 0 .75rem; font-size: .8125rem; color: var(--t2); }
    .ppf__grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); gap: 1rem 1.25rem; }
  `],
})
export class ProjectPeopleFieldsComponent {
  private readonly peopleSvc = inject(ProjectPeopleService);

  readonly value = model.required<ProjectPeopleValue>();
  /** The workspace's Project Managers list (null while unknown). */
  readonly pmOptions = input<PersonOption[] | null>(null);
  /** False for a caller who reaches the project only through a project role (D4). */
  readonly canAddProjectManagers = input(true);
  /** The lead PM — never offered as an additional PM. */
  readonly leadPmUserId = input<string | null | undefined>(null);
  readonly idPrefix = input('pp');
  /** Shows "at least one sponsor" after a save attempt. */
  readonly showErrors = input(false);
  readonly disabled = input(false);

  readonly leadExclude = computed(() => (this.leadPmUserId() ? [this.leadPmUserId()!] : []));

  readonly lookup = (term: string) => this.peopleSvc.lookup(term);

  patch(change: Partial<ProjectPeopleValue>): void {
    this.value.update(v => ({ ...v, ...change }));
  }
}
