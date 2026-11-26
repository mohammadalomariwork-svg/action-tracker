import { ChangeDetectionStrategy, Component, computed, forwardRef, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { of, switchMap } from 'rxjs';
import { ProjectClassification } from '../../../models/project-classification.model';
import { ProjectClassificationService } from '../../../services/project-classification.service';

/** The project's own classification, as the project response carries it. */
export interface CurrentProjectClassification {
  id: string | null | undefined;
  name: string | null | undefined;
  isActive: boolean | null | undefined;
}

/**
 * `<app-project-classification-select>` (WO-2026-11 P-1) — the optional
 * "Internal classification" dropdown of the project forms. A form control
 * (`formControlName` or `[(ngModel)]`), value = classification id or null.
 *
 * Options: "None" plus the workspace's **active** classifications in their
 * order. The project's current one is always offered too, marked "(inactive)"
 * when it has been deactivated, so a save can keep it (the server accepts the
 * classification a project already has). Loads when `workspaceId` is set;
 * changing the workspace reloads the options — the parent clears the value.
 */
@Component({
  selector: 'app-project-classification-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => ProjectClassificationSelectComponent),
    multi: true,
  }],
  template: `
    <label class="form-label" [for]="inputId()">{{ label() }}</label>
    <select class="form-select" [id]="inputId()"
            [disabled]="isDisabled() || !workspaceId()"
            [attr.aria-describedby]="hintId()"
            (change)="onSelect($any($event.target).value)"
            (blur)="onTouched()">
      <option value="" [selected]="!value()">None</option>
      @for (option of choices(); track option.id) {
        <option [value]="option.id" [selected]="option.id === value()">{{ option.label }}</option>
      }
    </select>
    <div class="pcs-hint" [id]="hintId()">
      @if (!workspaceId()) {
        Choose the workspace first.
      } @else if (loaded() && activeCount() === 0) {
        This workspace has no internal classifications. Workspace managers add them on the workspace settings page.
      } @else {
        Optional. Defined by this workspace, next to the global classification.
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .pcs-hint { margin-top: .25rem; font-size: .8125rem; color: var(--t2); }
  `],
})
export class ProjectClassificationSelectComponent implements ControlValueAccessor {
  private readonly service = inject(ProjectClassificationService);

  readonly workspaceId = input<string | null | undefined>(null);
  /** The classification the edited project holds now (null on create). */
  readonly current = input<CurrentProjectClassification | null>(null);
  readonly inputId = input.required<string>();
  readonly label = input('Internal classification');

  readonly value = signal<string | null>(null);
  readonly isDisabled = signal(false);
  readonly hintId = computed(() => `${this.inputId()}-hint`);

  private readonly options = toSignal(
    toObservable(this.workspaceId).pipe(
      switchMap(id => (id ? this.service.options(id) : of<ProjectClassification[] | null>(null))),
    ),
    { initialValue: null },
  );

  readonly loaded = computed(() => this.options() !== null);
  readonly activeCount = computed(() => (this.options() ?? []).filter(o => o.isActive).length);

  /** Active options in order, plus the project's current one (also when inactive or not loaded). */
  readonly choices = computed(() => {
    const list = (this.options() ?? []).filter(o => o.isActive).map(o => ({ id: o.id, label: o.name }));
    const current = this.current();
    if (current?.id && !list.some(o => o.id === current.id)) {
      const known = (this.options() ?? []).find(o => o.id === current.id);
      const name = known?.name ?? current.name ?? 'Current classification';
      const inactive = known ? !known.isActive : current.isActive === false;
      list.push({ id: current.id, label: inactive ? `${name} (inactive)` : name });
    }
    return list;
  });

  private onChange: (value: string | null) => void = () => {};
  onTouched: () => void = () => {};

  onSelect(raw: string): void {
    const next = raw || null;
    this.value.set(next);
    this.onChange(next);
  }

  writeValue(value: string | null | undefined): void {
    this.value.set(value || null);
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.isDisabled.set(disabled);
  }
}
