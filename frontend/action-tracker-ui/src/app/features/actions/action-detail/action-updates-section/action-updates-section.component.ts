import {
  ChangeDetectionStrategy, Component, DestroyRef, Injector,
  afterNextRender, computed, effect, inject, input, output, signal, untracked, viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';

import { ToastService } from '../../../../core/services/toast.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { ActionUpdateService } from '../../../../services/action-update.service';
import {
  ACTION_UPDATE_MAX_LENGTH, ActionUpdate, ActionUpdateList,
} from '../../../../models/action-update.model';
import { HasPermissionDirective } from '../../../../shared';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

/**
 * Action Updates (WO-2026-11 A-3) — the log that replaced comments on action
 * items and sub-items. Used on the action detail page (item level) and, in
 * the compact form, under each sub-item row.
 *
 * Rules (Q11), enforced by the API:
 *  - "Save draft" keeps an update only its author can see, edit, submit or delete;
 *  - "Submit" is one-way and asks for confirmation — a submitted update cannot
 *    be changed or deleted by anyone (409);
 *  - updates are allowed whatever the action item's status.
 * Reads need `ActionItems.View`; the form and the draft buttons need
 * `ActionItems.Edit`. The comments section used elsewhere is not affected (Q12).
 */
@Component({
  selector: 'app-action-updates-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, HasPermissionDirective, ConfirmDialogComponent],
  templateUrl: './action-updates-section.component.html',
  styleUrl: './action-updates-section.component.scss',
})
export class ActionUpdatesSectionComponent {
  readonly actionItemId = input.required<string>();
  /** Set for the updates of one sub-item; null for the action item itself. */
  readonly subItemId    = input<string | null>(null);
  /** The sub-item's title, for headings and labels. */
  readonly subItemTitle = input<string | null>(null);
  /** Compact form used inside a sub-item row (no card, smaller heading). */
  readonly compact      = input(false);
  /** Any change re-loads the list (the page passes the reloaded action item, e.g. after a direction). */
  readonly reloadToken  = input<unknown>(null);

  /** Updates the caller can see (submitted + own drafts), whenever the list changes. */
  readonly countChange = output<number>();

  private readonly service    = inject(ActionUpdateService);
  private readonly toast      = inject(ToastService);
  private readonly permState  = inject(PermissionStateService);
  private readonly injector   = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  private readonly submitDialog = viewChild<ConfirmDialogComponent>('submitDialog');
  private readonly deleteDialog = viewChild<ConfirmDialogComponent>('deleteDialog');

  readonly maxLength = ACTION_UPDATE_MAX_LENGTH;

  // ── State ───────────────────────────────────────────────────────────────
  readonly list       = signal<ActionUpdateList | null>(null);
  readonly loading    = signal(true);
  readonly loadFailed = signal(false);
  /** 'draft' / 'submit' while the new-update form is saving. */
  readonly saving     = signal<'draft' | 'submit' | null>(null);
  /** Id of the draft whose action is in flight. */
  readonly busyId     = signal<string | null>(null);

  readonly newText      = signal('');
  readonly newImportant = signal(false);

  readonly editingId     = signal<string | null>(null);
  readonly editText      = signal('');
  readonly editImportant = signal(false);

  /** What the open confirm dialog is about: the new form, or a draft. */
  readonly pendingSubmit = signal<'new' | ActionUpdate | null>(null);
  readonly pendingDelete = signal<ActionUpdate | null>(null);
  private  returnFocusTo: HTMLElement | null = null;

  readonly items     = computed(() => this.list()?.items ?? []);
  readonly drafts    = computed(() => this.items().filter(u => u.isDraft));
  readonly submitted = computed(() => this.items().filter(u => !u.isDraft));

  private readonly permissions = toSignal(this.permState.permissions$, { initialValue: [] });
  readonly canView = computed(() => { this.permissions(); return this.permState.hasPermission('ActionItems', 'View'); });

  /** Unique element-id prefix: the page holds one item-level and several sub-item sections. */
  readonly idPrefix = computed(() => `au-${this.subItemId() ?? 'item'}`);

  readonly newLength = computed(() => this.newText().trim().length);
  readonly newValid  = computed(() => this.newLength() > 0 && this.newLength() <= this.maxLength);
  readonly editValid = computed(() => {
    const n = this.editText().trim().length;
    return n > 0 && n <= this.maxLength;
  });

  readonly heading = computed(() => {
    const n = this.items().length;
    return this.compact() ? `Updates (${n})` : `Action Updates (${n})`;
  });

  readonly submitMessage = computed(() => {
    const where = this.subItemTitle() ? ` on the sub-item “${this.subItemTitle()}”` : '';
    return `Submit this action update${where}? Everyone who can see this action item will see it, ` +
           'and it cannot be changed or deleted afterwards — by anyone.';
  });

  constructor() {
    effect(() => {
      this.actionItemId(); this.subItemId(); this.reloadToken();
      if (!this.canView()) { this.loading.set(false); return; }
      untracked(() => this.load());
    });
  }

  // ── Loading ─────────────────────────────────────────────────────────────
  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.service.list(this.actionItemId(), this.subItemId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: r => {
          this.setList(r.data);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  private setList(list: ActionUpdateList | null): void {
    this.list.set(list);
    this.countChange.emit(list?.items.length ?? 0);
  }

  /** Puts a changed / new update in its place: drafts first, then submitted (newest first). */
  private upsert(update: ActionUpdate): void {
    const current = this.list();
    if (!current) { this.load(); return; }
    const rest = current.items.filter(u => u.id !== update.id);
    const drafts = [update, ...rest].filter(u => u.isDraft)
      .sort((a, b) => (b.updatedAt ?? b.createdAt).localeCompare(a.updatedAt ?? a.createdAt));
    const submitted = [update, ...rest].filter(u => !u.isDraft)
      .sort((a, b) => (b.submittedAt ?? b.createdAt).localeCompare(a.submittedAt ?? a.createdAt));
    const items = [...drafts, ...submitted];
    this.setList({ ...current, items, draftCount: drafts.length, submittedCount: submitted.length });
  }

  private remove(updateId: string): void {
    const current = this.list();
    if (!current) return;
    const items = current.items.filter(u => u.id !== updateId);
    this.setList({
      ...current, items,
      draftCount: items.filter(u => u.isDraft).length,
      submittedCount: items.filter(u => !u.isDraft).length,
    });
  }

  // ── New update ──────────────────────────────────────────────────────────
  saveNewDraft(): void {
    if (!this.newValid() || this.saving()) return;
    this.createNew(false);
  }

  askSubmitNew(trigger: HTMLElement): void {
    if (!this.newValid() || this.saving()) return;
    this.pendingSubmit.set('new');
    this.returnFocusTo = trigger;
    this.submitDialog()?.open();
  }

  private createNew(submit: boolean): void {
    this.saving.set(submit ? 'submit' : 'draft');
    this.service.create(this.actionItemId(), this.subItemId(), {
      content: this.newText().trim(),
      isHighImportance: this.newImportant(),
      submit,
    }).subscribe({
      next: r => {
        this.saving.set(null);
        this.newText.set('');
        this.newImportant.set(false);
        if (r?.data) this.upsert(r.data); else this.load();
        this.toast.success(submit ? 'Action update submitted.' : 'Draft saved. Only you can see it.');
        this.returnFocusTo = null;
        afterNextRender(() => this.focus(`${this.idPrefix()}-new`), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(null);
        this.reportError(err, submit ? 'Could not submit the update.' : 'Could not save the draft.');
        this.restoreFocus();
      },
    });
  }

  // ── Drafts ──────────────────────────────────────────────────────────────
  startEdit(u: ActionUpdate): void {
    this.editingId.set(u.id);
    this.editText.set(u.content);
    this.editImportant.set(u.isHighImportance);
    afterNextRender(() => this.focus(`${this.idPrefix()}-edit-${u.id}`), { injector: this.injector });
  }

  cancelEdit(u: ActionUpdate): void {
    this.editingId.set(null);
    afterNextRender(() => this.focus(`${this.idPrefix()}-editbtn-${u.id}`), { injector: this.injector });
  }

  saveEdit(u: ActionUpdate): void {
    if (!this.editValid() || this.busyId()) return;
    this.busyId.set(u.id);
    this.service.update(this.actionItemId(), this.subItemId(), u.id, {
      content: this.editText().trim(),
      isHighImportance: this.editImportant(),
    }).subscribe({
      next: r => {
        this.busyId.set(null);
        this.editingId.set(null);
        if (r?.data) this.upsert(r.data);
        this.toast.success('Draft updated.');
        afterNextRender(() => this.focus(`${this.idPrefix()}-editbtn-${u.id}`), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        this.reportError(err, 'Could not update the draft.');
      },
    });
  }

  askSubmitDraft(u: ActionUpdate, trigger: HTMLElement): void {
    this.pendingSubmit.set(u);
    this.returnFocusTo = trigger;
    this.submitDialog()?.open();
  }

  onSubmitConfirmed(confirmed: boolean): void {
    const pending = this.pendingSubmit();
    this.pendingSubmit.set(null);
    if (!confirmed || !pending) { this.restoreFocus(); return; }
    if (pending === 'new') { this.createNew(true); return; }

    this.busyId.set(pending.id);
    this.service.submit(this.actionItemId(), this.subItemId(), pending.id).subscribe({
      next: r => {
        this.busyId.set(null);
        if (r?.data) this.upsert(r.data); else this.load();
        this.toast.success('Action update submitted.');
        this.returnFocusTo = null;
        afterNextRender(() => this.focus(`${this.idPrefix()}-entry-${pending.id}`), { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        this.reportError(err, 'Could not submit the update.');
        this.restoreFocus();
      },
    });
  }

  askDelete(u: ActionUpdate, trigger: HTMLElement): void {
    this.pendingDelete.set(u);
    this.returnFocusTo = trigger;
    this.deleteDialog()?.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    const u = this.pendingDelete();
    this.pendingDelete.set(null);
    if (!confirmed || !u) { this.restoreFocus(); return; }

    this.busyId.set(u.id);
    this.service.delete(this.actionItemId(), this.subItemId(), u.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.remove(u.id);
        this.toast.success('Draft deleted.');
        this.returnFocusTo = null;
        afterNextRender(() => this.focus(`${this.idPrefix()}-new`) || this.focus(`${this.idPrefix()}-heading`),
          { injector: this.injector });
      },
      error: (err: HttpErrorResponse) => {
        this.busyId.set(null);
        this.reportError(err, 'Could not delete the draft.');
        this.restoreFocus();
      },
    });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────
  private focus(id: string): boolean {
    const el = document.getElementById(id) as HTMLElement | null;
    if (!el || (el as HTMLButtonElement).disabled) return false;
    el.focus();
    return true;
  }

  private restoreFocus(): void {
    const target = this.returnFocusTo;
    this.returnFocusTo = null;
    if (target) afterNextRender(() => {
      if (document.contains(target) && !(target as HTMLButtonElement).disabled) target.focus();
      else this.focus(`${this.idPrefix()}-heading`);
    }, { injector: this.injector });
  }

  private errorText(err: HttpErrorResponse, fallback: string): string {
    const body = err?.error;
    if (typeof body?.message === 'string' && body.message) return body.message;
    if (body?.errors && typeof body.errors === 'object') {
      const all = Object.values(body.errors as Record<string, string[]>).flat().filter(Boolean);
      if (all.length) return all.join(' ');
    }
    return fallback;
  }

  /**
   * The error interceptor already toasts a 400 with its message and a 403 / 404;
   * a 409 (the update was submitted meanwhile) only reaches it as
   * "Unexpected error (409)", so give the reason and reload the list.
   */
  private reportError(err: HttpErrorResponse, fallback: string): void {
    if (err.status === 409) {
      this.toast.error(this.errorText(err, fallback));
      this.editingId.set(null);
      this.load();
    } else if (err.status === 404) {
      this.load();
    } else if (err.status !== 400 && err.status !== 403) {
      this.toast.error(fallback);
    }
  }

  initial(name: string | null | undefined): string {
    return (name ?? '?').trim().charAt(0).toUpperCase() || '?';
  }

  setNewText(event: Event): void {
    this.newText.set((event.target as HTMLTextAreaElement).value);
  }

  setEditText(event: Event): void {
    this.editText.set((event.target as HTMLTextAreaElement).value);
  }
}
