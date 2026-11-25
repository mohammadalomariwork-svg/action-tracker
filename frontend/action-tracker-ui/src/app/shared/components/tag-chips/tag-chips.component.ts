import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TagInfo } from '../../../core/models/action-item.model';

/**
 * `<app-tag-chips [tags]="item.tags" [max]="3" />` — read-only tag chips of an
 * action item (WO-2026-11 A-5) on list rows and the detail page. Renders
 * nothing without tags; beyond `max` it shows "+n" with the rest in its title.
 */
@Component({
  selector: 'app-tag-chips',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (tags()?.length) {
      <ul class="tch" [attr.aria-label]="'Tags: ' + allNames()">
        @for (t of shown(); track t.id) {
          <li class="tch__chip"><i class="bi bi-tag" aria-hidden="true"></i>{{ t.name }}</li>
        }
        @if (hidden().length) {
          <li class="tch__chip tch__chip--more" [attr.title]="hiddenNames()">+{{ hidden().length }}</li>
        }
      </ul>
    }
  `,
  styles: [`
    :host { display: inline-flex; min-width: 0; max-width: 100%; }
    .tch { display: flex; flex-wrap: wrap; gap: 0.25rem; margin: 0; padding: 0; list-style: none; min-width: 0; }
    .tch__chip {
      display: inline-flex; align-items: center; gap: 0.25rem; max-width: 100%;
      padding: 0 0.45rem; border-radius: 999px;
      border: 1px solid rgba(var(--brand-blue-r), 0.35); background: rgba(var(--brand-blue-r), 0.1);
      color: var(--t1); font-size: 11px; font-weight: 600; line-height: 1.7;
      overflow-wrap: anywhere;
    }
    .tch__chip i { color: var(--accent); font-size: 10px; }
    .tch__chip--more { border-color: var(--bd); background: var(--el); color: var(--t2); }
  `],
})
export class TagChipsComponent {
  readonly tags = input<TagInfo[] | null | undefined>([]);
  /** Chips shown before "+n" (0 = all). */
  readonly max  = input(0);

  readonly shown  = computed(() => { const t = this.tags() ?? []; return this.max() > 0 ? t.slice(0, this.max()) : t; });
  readonly hidden = computed(() => { const t = this.tags() ?? []; return this.max() > 0 ? t.slice(this.max()) : []; });
  readonly allNames    = computed(() => (this.tags() ?? []).map(t => t.name).join(', '));
  readonly hiddenNames = computed(() => this.hidden().map(t => t.name).join(', '));
}
