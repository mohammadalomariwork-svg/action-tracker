import {
  ChangeDetectionStrategy, Component, DestroyRef, Injector,
  afterNextRender, computed, effect, inject, input, output, signal, untracked, viewChild,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';

import { ToastService } from '../../../../core/services/toast.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { ActionUpdateService } from '../../../../services/action-update.service';
import { DocumentService } from '../../../../core/services/document.service';
import {
  ACTION_UPDATE_MAX_FILES, ACTION_UPDATE_MAX_LENGTH, ActionUpdate, ActionUpdateList,
} from '../../../../models/action-update.model';
import { HasPermissionDirective } from '../../../../shared';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';
import { showErrorHere } from '../../../../core/http/server-reason';

/** File rules shared with the server (`DocumentService`): 10 MB, these types (Q13). */
const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx'];
const MAX_FILE_BYTES = 10 * 1024 * 1024;

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
 *
 * Files (WO-2026-11 A-4): "Attach files" stages up to 10 files in the form.
 * Save draft creates the draft and uploads them; Submit creates a draft,
 * uploads them, then submits — so an update is never submitted without its
 * files (files cannot be added after submit). If an upload fails the update
 * stays a draft with the files that arrived. Each update has a "Files (n)"
 * toggle; only the author's draft can gain or lose files.
 */
@Component({
  selector: 'app-action-updates-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, HasPermissionDirective, ConfirmDialogComponent, DocumentsSectionComponent],
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
  private readonly docSvc     = inject(DocumentService);
  private readonly toast      = inject(ToastService);
  private readonly permState  = inject(PermissionStateService);
  private readonly injector   = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  private readonly submitDialog = viewChild<ConfirmDialogComponent>('submitDialog');
  private readonly deleteDialog = viewChild<ConfirmDialogComponent>('deleteDialog');

  readonly maxLength = ACTION_UPDATE_MAX_LENGTH;
  readonly maxFiles  = ACTION_UPDATE_MAX_FILES;
  readonly accept    = ALLOWED_EXTENSIONS.join(',');

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
  /** Files picked in the form, uploaded when the update is saved (A-4). */
  readonly staged       = signal<File[]>([]);
  readonly stagedError  = signal<string | null>(null);
  /** Updates whose files panel is open, and counts reported by open panels. */
  readonly openFiles    = signal<ReadonlySet<string>>(new Set());
  readonly fileCounts   = signal<ReadonlyMap<string, number>>(new Map());

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
  readonly canEditPerm = computed(() => { this.permissions(); return this.permState.hasPermission('ActionItems', 'Edit'); });

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
    const pending = this.pendingSubmit();
    const files = pending === 'new' ? this.staged().length : (pending?.documentCount ?? 0);
    const withFiles = files === 0 ? '' : ` with its ${files === 1 ? 'file' : files + ' files'}`;
    return `Submit this action update${where}${withFiles}? Everyone who can see this action item will see it, ` +
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
    if (this.staged().length > 0) { void this.createWithFiles(submit); return; }
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

  /**
   * New update with staged files: create as a draft, upload the files, then
   * submit when asked — files cannot be added once an update is submitted.
   * An upload failure leaves the draft (with the files that arrived) for the
   * author to finish.
   */
  private async createWithFiles(submit: boolean): Promise<void> {
    const files = [...this.staged()];
    this.saving.set(submit ? 'submit' : 'draft');
    let created: ActionUpdate | null = null;
    let failedFile: string | null = null;
    try {
      created = (await firstValueFrom(this.service.create(this.actionItemId(), this.subItemId(), {
        content: this.newText().trim(), isHighImportance: this.newImportant(), submit: false,
      }))).data;
      for (const file of files) {
        failedFile = file.name;
        await firstValueFrom(this.docSvc.upload('ActionItemUpdate', created.id, baseName(file.name), file));
      }
      failedFile = null;
      if (submit) created = (await firstValueFrom(this.service.submit(this.actionItemId(), this.subItemId(), created.id))).data;

      this.saving.set(null);
      this.resetForm();
      this.upsert({ ...created, documentCount: files.length });
      this.toast.success(submit ? 'Action update submitted with its files.' : 'Draft saved with its files. Only you can see it.');
      this.returnFocusTo = null;
      afterNextRender(() => this.focus(`${this.idPrefix()}-new`), { injector: this.injector });
    } catch (err) {
      this.saving.set(null);
      if (!created) {
        this.reportError(err as HttpErrorResponse, submit ? 'Could not submit the update.' : 'Could not save the draft.');
        this.restoreFocus();
        return;
      }
      // The draft exists: keep it, say what did not happen, and show it.
      this.resetForm();
      // This toast carries the server's reason, so the error interceptor stays silent.
      const reason = showErrorHere(err);
      this.toast.error(failedFile
        ? `The update was saved as a draft, but “${failedFile}” could not be attached${reason ? ': ' + reason : '.'} It was not submitted.`
        : `The update and its files were saved as a draft, but it could not be submitted${reason ? ': ' + reason : '.'}`);
      this.load();
    }
  }

  private resetForm(): void {
    this.newText.set('');
    this.newImportant.set(false);
    this.staged.set([]);
    this.stagedError.set(null);
  }

  // ── Staged files (new update) ───────────────────────────────────────────
  addFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);
    input.value = '';
    const problems: string[] = [];
    const next = [...this.staged()];
    for (const file of picked) {
      const ext = file.name.includes('.') ? file.name.slice(file.name.lastIndexOf('.')).toLowerCase() : '';
      if (!ALLOWED_EXTENSIONS.includes(ext)) problems.push(`${file.name}: only PDF, Word, Excel and PowerPoint files`);
      else if (file.size > MAX_FILE_BYTES) problems.push(`${file.name}: larger than 10 MB`);
      else if (file.size === 0) problems.push(`${file.name}: empty file`);
      else if (next.length >= this.maxFiles) problems.push(`${file.name}: at most ${this.maxFiles} files per update`);
      else next.push(file);
    }
    this.staged.set(next);
    this.stagedError.set(problems.length ? 'Not attached — ' + problems.join('; ') + '.' : null);
  }

  removeStaged(index: number): void {
    this.staged.set(this.staged().filter((_, i) => i !== index));
    this.stagedError.set(null);
    afterNextRender(() => this.focus(`${this.idPrefix()}-attach`), { injector: this.injector });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // ── Files of an existing update ─────────────────────────────────────────
  fileCount(u: ActionUpdate): number {
    return this.fileCounts().get(u.id) ?? u.documentCount ?? 0;
  }

  filesOpen(updateId: string): boolean {
    return this.openFiles().has(updateId);
  }

  toggleFiles(updateId: string): void {
    const next = new Set(this.openFiles());
    if (next.has(updateId)) next.delete(updateId); else next.add(updateId);
    this.openFiles.set(next);
  }

  setFileCount(updateId: string, count: number): void {
    if (this.fileCounts().get(updateId) === count) return;
    const next = new Map(this.fileCounts());
    next.set(updateId, count);
    this.fileCounts.set(next);
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
        if (this.filesOpen(pending.id)) this.toggleFiles(pending.id);   // reopen → read-only list
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
   * The error interceptor toasts a 400 / 403 / 404 itself; a 409 (the update was
   * submitted meanwhile) is toasted here with the same reason (ngx-toastr drops the
   * repeat) and reloads the list.
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

/** "minutes.pdf" → "minutes" (the document's display name). */
function baseName(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return (dot > 0 ? fileName.slice(0, dot) : fileName).slice(0, 255);
}
