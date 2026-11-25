import {
  ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, computed, effect,
  inject, input, model, signal, untracked, viewChild,
} from '@angular/core';

import { ActionItemTagService } from '../../../services/action-item-tag.service';
import { PermissionStateService } from '../../../features/permissions/services/permission-state.service';
import { TAG_MAX_LENGTH, TAG_MAX_PER_ITEM, WorkspaceTag } from '../../../models/action-item-tag.model';
import { cleanTag, singleTagProblem, tagKey } from '../../utils/tag-rules.util';

let nextId = 0;

/**
 * `<app-tag-input [(tags)]="tags" [workspaceId]="wsId" />` — tag chips with a
 * text box (WO-2026-11 A-5). Enter or comma adds the typed tag (or the
 * highlighted suggestion), Backspace in an empty box removes the last chip,
 * each chip has a remove button. Suggestions are the workspace's own tags
 * (`GET /workspaces/:id/tags`, loaded on first focus, only with
 * `ActionItems.View`), shown as a listbox under the box (arrow keys, Enter,
 * Esc). The rules (10 tags, 30 characters, no commas, duplicates ignored
 * whatever the case) come from `tag-rules.util.ts`; the server decides on
 * save. `disabled` shows the chips read-only with `lockedNote`.
 */
@Component({
  selector: 'app-tag-input',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tag-input.component.html',
  styleUrl: './tag-input.component.scss',
})
export class TagInputComponent {
  private readonly tagSvc   = inject(ActionItemTagService);
  private readonly perms    = inject(PermissionStateService);
  private readonly injector = inject(Injector);

  /** The tag names (two-way). */
  readonly tags        = model<string[]>([]);
  /** Workspace whose tags are suggested; none = no suggestions. */
  readonly workspaceId = input<string | null | undefined>(null);
  readonly inputId     = input(`tag-input-${++nextId}`);
  readonly label       = input('Tags');
  readonly disabled    = input(false);
  readonly lockedNote  = input<string | null>(null);

  readonly max       = TAG_MAX_PER_ITEM;
  readonly maxLength = TAG_MAX_LENGTH;

  readonly text   = signal('');
  readonly open   = signal(false);
  readonly active = signal(-1);
  readonly error  = signal<string | null>(null);
  readonly status = signal('');

  private readonly workspaceTags = signal<WorkspaceTag[]>([]);
  private loadedFor: string | null = null;

  private readonly box = viewChild<ElementRef<HTMLInputElement>>('box');

  readonly listId = computed(() => `${this.inputId()}-list`);
  readonly hintId = computed(() => `${this.inputId()}-hint`);
  readonly atCap  = computed(() => this.tags().length >= TAG_MAX_PER_ITEM);

  readonly suggestions = computed(() => {
    const chosen = new Set(this.tags().map(t => tagKey(cleanTag(t))));
    const query  = tagKey(cleanTag(this.text()));
    return this.workspaceTags()
      .filter(t => !chosen.has(tagKey(t.name)) && (!query || tagKey(t.name).includes(query)))
      .slice(0, 8);
  });
  readonly listOpen = computed(() => this.open() && !this.disabled() && this.suggestions().length > 0);
  readonly activeId = computed(() =>
    this.listOpen() && this.active() >= 0 ? `${this.inputId()}-opt-${this.active()}` : null);

  constructor() {
    // Another workspace = other suggestions (tags never cross workspaces).
    effect(() => {
      this.workspaceId();
      untracked(() => {
        this.loadedFor = null;
        this.workspaceTags.set([]);
      });
    });
  }

  optionId(i: number): string { return `${this.inputId()}-opt-${i}`; }

  focusBox(): void { this.box()?.nativeElement.focus(); }

  onFocus(): void {
    this.open.set(true);
    this.loadSuggestions();
  }

  onInput(value: string): void {
    this.error.set(null);
    // A comma (typed or pasted) separates tags.
    if (value.includes(',')) {
      const parts = value.split(',');
      const rest  = parts.pop() ?? '';
      for (const part of parts) if (!this.add(part)) break;
      this.text.set(this.error() ? value.replace(/,+$/, '') : rest);
      this.syncBox();
    } else {
      this.text.set(value);
    }
    this.open.set(true);
    this.active.set(-1);
  }

  onKeydown(event: KeyboardEvent): void {
    const list = this.suggestions();
    switch (event.key) {
      case 'Enter':
        event.preventDefault();                         // never submits the surrounding form
        if (this.listOpen() && this.active() >= 0) this.choose(list[this.active()]);
        else this.commitText();
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
        if (this.listOpen()) {                          // close the list only — not the drawer around it
          event.preventDefault();
          event.stopPropagation();
          this.open.set(false);
          this.active.set(-1);
        }
        break;
      case 'Backspace':
        if (!this.text() && this.tags().length) {
          event.preventDefault();
          this.remove(this.tags().length - 1, false);
        }
        break;
    }
  }

  onBlur(): void {
    // Typed but not added yet: keep it rather than losing it on Save.
    if (cleanTag(this.text())) this.commitText();
    this.open.set(false);
    this.active.set(-1);
  }

  choose(suggestion: WorkspaceTag): void {
    if (this.add(suggestion.name)) {
      this.text.set('');
      this.syncBox();
    }
    this.active.set(-1);
    this.focusBox();
  }

  remove(index: number, refocus = true): void {
    const name = this.tags()[index];
    this.tags.update(list => list.filter((_, i) => i !== index));
    this.error.set(null);
    this.status.set(`Tag ${name} removed.`);
    if (refocus) afterNextRender(() => this.focusBox(), { injector: this.injector });
  }

  private commitText(): void {
    if (this.add(this.text())) {
      this.text.set('');
      this.syncBox();
    }
  }

  /** Adds one tag; false (with a message) when a rule refuses it. Empty text is ignored. */
  private add(raw: string): boolean {
    const name = cleanTag(raw);
    if (!name) return true;
    const problem = singleTagProblem(name);
    if (problem) { this.error.set(problem); return false; }
    if (this.tags().some(t => tagKey(cleanTag(t)) === tagKey(name))) {
      this.error.set(`"${name}" is already added.`);
      return false;
    }
    if (this.atCap()) { this.error.set(`An action item can have at most ${TAG_MAX_PER_ITEM} tags.`); return false; }

    // Reuse the workspace's spelling when the name already exists there.
    const existing = this.workspaceTags().find(t => tagKey(t.name) === tagKey(name));
    const value = existing?.name ?? name;
    this.tags.update(list => [...list, value]);
    this.error.set(null);
    this.status.set(`Tag ${value} added.`);
    return true;
  }

  private syncBox(): void {
    const el = this.box()?.nativeElement;
    if (el && el.value !== this.text()) el.value = this.text();
  }

  private loadSuggestions(): void {
    const ws = this.workspaceId();
    if (!ws || this.loadedFor === ws || this.disabled()) return;
    if (!this.perms.hasPermission('ActionItems', 'View')) return;
    this.loadedFor = ws;
    this.tagSvc.workspaceTags(ws).subscribe({
      next: r => { if (this.loadedFor === ws) this.workspaceTags.set(r.data ?? []); },
      error: () => { if (this.loadedFor === ws) this.workspaceTags.set([]); },   // typing still works
    });
  }
}
