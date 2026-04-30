import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  Input,
  computed,
  forwardRef,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** Minimal shape both `OrgUnit` and `OrgUnitDropdownItem` satisfy. */
export interface OrgUnitOption {
  id: string;
  name: string;
  level: number;
  code?: string;
}

@Component({
  selector: 'app-org-unit-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './org-unit-select.component.html',
  styleUrl: './org-unit-select.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => OrgUnitSelectComponent),
      multi: true,
    },
  ],
})
export class OrgUnitSelectComponent implements ControlValueAccessor {
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  @Input() set options(value: ReadonlyArray<OrgUnitOption>) {
    this._options.set(value ?? []);
  }
  get options(): ReadonlyArray<OrgUnitOption> {
    return this._options();
  }

  @Input() placeholder = 'Select…';
  /** When true, an explicit "no value" entry is shown at the top of the panel. */
  @Input() allowEmpty = false;
  @Input() emptyLabel = '— None —';
  @Input() invalid = false;
  /** Optional ID for the trigger button (so external `<label for>` works). */
  @Input() triggerId?: string;
  /** Bootstrap form-control sizing modifier: 'sm' | 'lg' | undefined. */
  @Input() size?: 'sm' | 'lg';

  private readonly _options = signal<ReadonlyArray<OrgUnitOption>>([]);
  protected readonly value = signal<string>('');
  protected readonly disabled = signal(false);
  protected readonly open = signal(false);
  protected readonly search = signal('');

  protected readonly selected = computed<OrgUnitOption | null>(() => {
    const id = this.value();
    if (!id) return null;
    return this._options().find(o => o.id === id) ?? null;
  });

  protected readonly filtered = computed<OrgUnitOption[]>(() => {
    const q = this.search().trim().toLowerCase();
    const list = this._options();
    if (!q) return [...list];
    return list.filter(o =>
      o.name.toLowerCase().includes(q) ||
      (o.code?.toLowerCase().includes(q) ?? false) ||
      `l${o.level}`.includes(q),
    );
  });

  // ── ControlValueAccessor ───────────────────────────────────────────────────
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string | null | undefined): void {
    this.value.set(value ?? '');
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
    if (isDisabled) this.open.set(false);
  }

  // ── UI ─────────────────────────────────────────────────────────────────────
  protected toggle(): void {
    if (this.disabled()) return;
    const next = !this.open();
    this.open.set(next);
    if (next) {
      this.search.set('');
      queueMicrotask(() => {
        const input = this.host.nativeElement.querySelector<HTMLInputElement>(
          '.ous-search input',
        );
        input?.focus();
      });
    } else {
      this.onTouched();
    }
  }

  protected pick(option: OrgUnitOption | null): void {
    const next = option?.id ?? '';
    if (next !== this.value()) {
      this.value.set(next);
      this.onChange(next);
    }
    this.open.set(false);
    this.onTouched();
  }

  protected indent(level: number): string {
    // 12px per level, capped so very deep trees don't overflow the panel.
    const px = Math.min(Math.max(level - 1, 0), 8) * 12;
    return `${px}px`;
  }

  protected triggerLabel(): string {
    const sel = this.selected();
    if (!sel) return this.placeholder;
    return this.formatLabel(sel);
  }

  protected formatLabel(o: OrgUnitOption): string {
    const code = o.code ? ` [${o.code}]` : '';
    return `${o.name}${code}`;
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.open()) return;
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (!this.open()) return;
    this.open.set(false);
    this.onTouched();
  }
}
