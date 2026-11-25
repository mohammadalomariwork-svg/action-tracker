import {
  ChangeDetectionStrategy, Component, ElementRef, HostListener, Injector, afterNextRender, computed,
  inject, input, model, signal, viewChild,
} from '@angular/core';

import { TagFilterOption } from '../../../models/action-item-tag.model';
import { tagKey, cleanTag } from '../../utils/tag-rules.util';

let nextId = 0;

/**
 * `<app-tag-filter [options]="opts" [(selected)]="tagIds" />` — "Tags" filter
 * for an action-item list (WO-2026-11 A-5): a button that opens a panel of
 * checkboxes, with a search box, grouped by `option.group` (My Actions: the
 * workspace). The list shows items with **any** of the chosen tags; the ids
 * go to the server as `tagIds`. Esc or a click outside closes the panel
 * (focus returns to the button after Esc).
 */
@Component({
  selector: 'app-tag-filter',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tag-filter.component.html',
  styleUrl: './tag-filter.component.scss',
})
export class TagFilterComponent {
  private readonly host     = inject(ElementRef<HTMLElement>);
  private readonly injector = inject(Injector);

  readonly options  = input<TagFilterOption[]>([]);
  readonly selected = model<string[]>([]);
  readonly loading  = input(false);
  readonly idPrefix = input(`tag-filter-${++nextId}`);

  readonly open   = signal(false);
  readonly search = signal('');

  private readonly trigger   = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  private readonly searchBox = viewChild<ElementRef<HTMLInputElement>>('searchBox');

  readonly panelId = computed(() => `${this.idPrefix()}-panel`);

  readonly groups = computed(() => {
    const q = tagKey(cleanTag(this.search()));
    const groups = new Map<string, TagFilterOption[]>();
    for (const o of this.options()) {
      if (q && !tagKey(o.name).includes(q) && !tagKey(o.group ?? '').includes(q)) continue;
      const key = o.group ?? '';
      groups.set(key, [...(groups.get(key) ?? []), o]);
    }
    return [...groups.entries()].map(([label, options]) => ({ label, options }));
  });

  readonly selectedNames = computed(() => {
    const ids = new Set(this.selected());
    return this.options().filter(o => ids.has(o.id))
      .map(o => (o.group ? `${o.name} (${o.group})` : o.name)).join(', ');
  });

  isSelected(id: string): boolean { return this.selected().includes(id); }

  toggle(): void {
    this.open.update(v => !v);
    if (this.open()) afterNextRender(() => this.searchBox()?.nativeElement.focus(), { injector: this.injector });
  }

  close(returnFocus: boolean): void {
    if (!this.open()) return;
    this.open.set(false);
    this.search.set('');
    if (returnFocus) afterNextRender(() => this.trigger()?.nativeElement.focus(), { injector: this.injector });
  }

  check(id: string, checked: boolean): void {
    this.selected.update(list => checked ? [...new Set([...list, id])] : list.filter(x => x !== id));
  }

  clear(): void {
    if (this.selected().length) this.selected.set([]);
  }

  onPanelKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.close(true);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close(false);
  }
}
