import {
  ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injector, afterNextRender, computed, inject,
  input, model, signal, viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, debounceTime, map, of, switchMap, tap } from 'rxjs';

import { PersonOption } from '../../../models/project-people.model';

let nextId = 0;

/** Characters typed before a remote search starts. */
export const PERSON_SEARCH_MIN = 2;

/**
 * `<app-person-picker [(selected)]="people" ... />` — chosen people as chips
 * plus a combobox (WO-2026-11 P-2). Options come from `options` (a short local
 * list, e.g. the workspace's Project Managers, filtered as you type) or, when
 * `options` is null, from `search` (e.g. `GET /projects/people-lookup`, after
 * {@link PERSON_SEARCH_MIN} characters). Arrow keys move through the list,
 * Enter picks, Esc closes the list (not the drawer around it), Backspace in
 * an empty box removes the last chip, each chip has a remove button. `disabled`
 * shows the chips read-only with `lockedNote`. The server decides on save.
 */
@Component({
  selector: 'app-person-picker',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './person-picker.component.html',
  styleUrl: './person-picker.component.scss',
})
export class PersonPickerComponent {
  private readonly injector = inject(Injector);

  /** The chosen people (two-way). */
  readonly selected    = model<PersonOption[]>([]);
  /** A local list to choose from; null = search with `search`. */
  readonly options     = input<PersonOption[] | null>(null);
  /** Remote search, used when `options` is null. */
  readonly search      = input<((term: string) => Observable<PersonOption[]>) | null>(null);
  /** Ids never offered (e.g. the lead PM in the additional-PM picker). */
  readonly exclude     = input<readonly string[]>([]);
  readonly inputId     = input(`person-picker-${++nextId}`);
  readonly label       = input('People');
  readonly required    = input(false);
  /** Shows the "required" message (after a save attempt). */
  readonly showErrors  = input(false);
  readonly requiredMessage = input('Choose at least one person.');
  readonly disabled    = input(false);
  readonly lockedNote  = input<string | null>(null);
  readonly hint        = input<string | null>(null);
  /** At most this many people (0 = no limit). */
  readonly max         = input(0);

  readonly text    = signal('');
  readonly open    = signal(false);
  readonly active  = signal(-1);
  readonly status  = signal('');
  readonly loading = signal(false);
  private readonly remote = signal<PersonOption[]>([]);
  private readonly searched = signal('');

  private readonly box = viewChild<ElementRef<HTMLInputElement>>('box');
  private readonly terms = new Subject<string>();

  readonly listId  = computed(() => `${this.inputId()}-list`);
  readonly hintId  = computed(() => `${this.inputId()}-hint`);
  readonly errorId = computed(() => `${this.inputId()}-error`);
  readonly atMax   = computed(() => this.max() > 0 && this.selected().length >= this.max());
  readonly isLocal = computed(() => this.options() !== null);
  readonly missing = computed(() => this.required() && this.showErrors() && this.selected().length === 0);

  /** What the list shows: not chosen, not excluded, matching the text. */
  readonly choices = computed(() => {
    const taken = new Set([...this.selected().map(p => p.userId.toLowerCase()), ...this.exclude().map(id => id.toLowerCase())]);
    const query = this.text().trim().toLowerCase();
    const pool  = this.isLocal() ? this.options()! : this.remote();
    return pool
      .filter(p => !taken.has(p.userId.toLowerCase()))
      .filter(p => !this.isLocal() || !query
        || p.fullName.toLowerCase().includes(query) || (p.email ?? '').toLowerCase().includes(query))
      .slice(0, 20);
  });

  /** "Type 2 characters" / "No one found" under the box while it is focused. */
  readonly emptyNote = computed(() => {
    if (!this.open() || this.atMax() || this.choices().length) return null;
    if (this.isLocal()) return (this.options() ?? []).length ? 'No one else to add.' : 'No one to choose from.';
    const term = this.text().trim();
    if (term.length < PERSON_SEARCH_MIN) return `Type at least ${PERSON_SEARCH_MIN} characters to search.`;
    if (this.loading() || this.searched() !== term) return 'Searching…';
    return 'No active user found.';
  });

  readonly listOpen = computed(() => this.open() && !this.disabled() && !this.atMax() && this.choices().length > 0);
  readonly activeId = computed(() =>
    this.listOpen() && this.active() >= 0 ? this.optionId(this.active()) : null);
  readonly describedBy = computed(() =>
    [this.hintId(), this.missing() ? this.errorId() : null].filter(Boolean).join(' '));

  constructor() {
    this.terms.pipe(
      debounceTime(250),
      tap(() => this.loading.set(true)),
      switchMap(term => {
        const fn = this.search();
        if (!fn || term.length < PERSON_SEARCH_MIN) return of({ term, list: [] as PersonOption[] });
        return fn(term).pipe(
          catchError(() => of([] as PersonOption[])),
          map(list => ({ term, list })),
        );
      }),
      takeUntilDestroyed(inject(DestroyRef)),
    ).subscribe(({ term, list }) => {
      this.loading.set(false);
      this.remote.set(list);
      this.searched.set(term);
      if (this.open() && term.length >= PERSON_SEARCH_MIN)
        this.status.set(list.length ? `${list.length} ${list.length === 1 ? 'person' : 'people'} found.` : 'No active user found.');
    });
  }

  optionId(i: number): string { return `${this.inputId()}-opt-${i}`; }

  initial(name: string): string { return (name?.trim()?.charAt(0) || '?').toUpperCase(); }

  focusBox(): void { this.box()?.nativeElement.focus(); }

  onFocus(): void { this.open.set(true); }

  onInput(value: string): void {
    this.text.set(value);
    this.open.set(true);
    this.active.set(-1);
    if (!this.isLocal()) this.terms.next(value.trim());
  }

  onKeydown(event: KeyboardEvent): void {
    const list = this.choices();
    switch (event.key) {
      case 'Enter':
        event.preventDefault();                         // never submits the surrounding form
        if (this.listOpen() && this.active() >= 0) this.choose(list[this.active()]);
        else if (this.listOpen() && list.length === 1) this.choose(list[0]);
        break;
      case 'ArrowDown':
        if (!list.length) return;
        event.preventDefault();
        this.open.set(true);
        this.active.set((this.active() + 1) % list.length);
        break;
      case 'ArrowUp':
        if (!list.length) return;
        event.preventDefault();
        this.open.set(true);
        this.active.set(this.active() <= 0 ? list.length - 1 : this.active() - 1);
        break;
      case 'Escape':
        if (this.open() && (this.listOpen() || this.emptyNote())) {   // close the list only — not the drawer around it
          event.preventDefault();
          event.stopPropagation();
          this.open.set(false);
          this.active.set(-1);
        }
        break;
      case 'Backspace':
        if (!this.text() && this.selected().length) {
          event.preventDefault();
          this.remove(this.selected().length - 1, false);
        }
        break;
    }
  }

  onBlur(): void {
    this.open.set(false);
    this.active.set(-1);
  }

  choose(person: PersonOption): void {
    if (this.atMax() || this.selected().some(p => p.userId.toLowerCase() === person.userId.toLowerCase())) return;
    this.selected.update(list => [...list, person]);
    this.status.set(`${person.fullName} added.`);
    this.text.set('');
    this.syncBox();
    this.active.set(-1);
    if (!this.isLocal()) { this.remote.set([]); this.searched.set(''); }
    this.focusBox();
  }

  remove(index: number, refocus = true): void {
    const person = this.selected()[index];
    if (!person) return;
    this.selected.update(list => list.filter((_, i) => i !== index));
    this.status.set(`${person.fullName} removed.`);
    if (refocus) afterNextRender(() => this.focusBox(), { injector: this.injector });
  }

  private syncBox(): void {
    const el = this.box()?.nativeElement;
    if (el && el.value !== this.text()) el.value = this.text();
  }
}
