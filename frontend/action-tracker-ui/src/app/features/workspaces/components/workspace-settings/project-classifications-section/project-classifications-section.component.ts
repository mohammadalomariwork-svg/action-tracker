import {
  ChangeDetectionStrategy, Component, Injector, afterNextRender, computed, effect, inject, input, signal, untracked, viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ProjectClassification, PROJECT_CLASSIFICATION_MAX_LENGTH } from '../../../../../models/project-classification.model';
import { ProjectClassificationService } from '../../../../../services/project-classification.service';
import { ConfirmDialogComponent } from '../../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { HasPermissionDirective } from '../../../../../shared';
import {
  classificationNameProblem, cleanClassificationName, duplicateClassificationProblem,
} from '../../../../../shared/utils/project-classification-rules.util';

/**
 * Workspace Settings → "Project Classifications" (WO-2026-11 P-1): the
 * workspace's internal project classifications. Everyone who can open the page
 * sees the list (order, name, Inactive badge, how many projects use each);
 * with `Workspaces.Edit` (`*appHasPermission`) a manager adds, renames
 * (inline, Enter saves, Esc cancels), moves up / down, deactivates /
 * reactivates and deletes. Delete asks through the shared confirm dialog and
 * is disabled while any project — also a deleted one — uses the
 * classification; the usage line says so. The server's reason for a refused
 * change (duplicate name, in use) is shown in the section.
 */
@Component({
  selector: 'app-project-classifications-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ConfirmDialogComponent, HasPermissionDirective],
  templateUrl: './project-classifications-section.component.html',
  styleUrl: './project-classifications-section.component.scss',
})
export class ProjectClassificationsSectionComponent {
  private readonly service = inject(ProjectClassificationService);
  private readonly injector = inject(Injector);

  readonly workspaceId = input.required<string>();

  readonly maxLength = PROJECT_CLASSIFICATION_MAX_LENGTH;
  readonly items = signal<ProjectClassification[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly busy = signal(false);

  readonly newName = signal('');
  readonly newNameProblem = signal<string | null>(null);

  readonly renamingId = signal<string | null>(null);
  readonly renameValue = signal('');
  readonly renameProblem = signal<string | null>(null);

  readonly pendingDelete = signal<ProjectClassification | null>(null);
  readonly deleteMessage = computed(() => {
    const c = this.pendingDelete();
    return c ? `Delete the classification "${c.name}"? No project uses it, and it can be added again later.` : '';
  });

  /** Polite announcement after a change (screen readers). */
  readonly announcement = signal('');

  private readonly deleteDialog = viewChild<ConfirmDialogComponent>('deleteDialog');

  private readonly loadOnWorkspace = effect(() => {
    const id = this.workspaceId();
    untracked(() => this.load(id));
  });

  // ── Load ──────────────────────────────────────────────────────────────────

  private load(workspaceId: string): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.service.list(workspaceId).subscribe({
      next: res => {
        this.items.set(res.data ?? []);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.loadError.set(reason(err) ?? 'The project classifications could not be loaded.');
        this.loading.set(false);
      },
    });
  }

  /** Re-reads the list without the loading state, so focus survives. */
  private refresh(after?: () => void): void {
    this.service.list(this.workspaceId()).subscribe({
      next: res => {
        this.items.set(res.data ?? []);
        after?.();
      },
      error: () => after?.(),
    });
  }

  usageText(c: ProjectClassification): string {
    const total = c.projectCount + c.deletedProjectCount;
    if (total === 0) return 'Not used by any project';
    const projects = total === 1 ? '1 project' : `${total} projects`;
    const deleted = c.deletedProjectCount === 0
      ? ''
      : c.projectCount === 0
        ? (c.deletedProjectCount === 1 ? ' (deleted)' : ' (all deleted)')
        : ` (${c.deletedProjectCount} deleted)`;
    return `Used by ${projects}${deleted} — cannot be deleted`;
  }

  // ── Add ───────────────────────────────────────────────────────────────────

  onNewNameInput(value: string): void {
    this.newName.set(value);
    if (this.newNameProblem()) this.newNameProblem.set(null);
  }

  add(event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    const problem = classificationNameProblem(this.newName())
      ?? duplicateClassificationProblem(this.newName(), this.items());
    if (problem) {
      this.newNameProblem.set(problem);
      this.focus('pcsNewName');
      return;
    }

    const name = cleanClassificationName(this.newName());
    this.busy.set(true);
    this.actionError.set(null);
    this.service.create(this.workspaceId(), { name }).subscribe({
      next: res => {
        this.busy.set(false);
        this.newName.set('');
        if (res.data) this.items.update(list => [...list, res.data]);
        this.announce(`Added "${name}".`);
        this.focus('pcsNewName');
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.newNameProblem.set(reason(err) ?? 'The classification could not be added.');
        this.focus('pcsNewName');
      },
    });
  }

  // ── Rename ────────────────────────────────────────────────────────────────

  startRename(c: ProjectClassification): void {
    this.renamingId.set(c.id);
    this.renameValue.set(c.name);
    this.renameProblem.set(null);
    this.actionError.set(null);
    this.focus(`pcsRenameInput-${c.id}`, true);
  }

  onRenameInput(value: string): void {
    this.renameValue.set(value);
    if (this.renameProblem()) this.renameProblem.set(null);
  }

  cancelRename(c: ProjectClassification): void {
    this.renamingId.set(null);
    this.renameProblem.set(null);
    this.focus(`pcsRename-${c.id}`);
  }

  saveRename(c: ProjectClassification, event?: Event): void {
    event?.preventDefault();
    if (this.busy()) return;
    const problem = classificationNameProblem(this.renameValue())
      ?? duplicateClassificationProblem(this.renameValue(), this.items(), c.id);
    if (problem) {
      this.renameProblem.set(problem);
      this.focus(`pcsRenameInput-${c.id}`);
      return;
    }
    const name = cleanClassificationName(this.renameValue());
    if (name === c.name) {
      this.cancelRename(c);
      return;
    }
    this.save(c, { name, isActive: c.isActive }, `Renamed to "${name}".`, () => {
      this.renamingId.set(null);
      this.focus(`pcsRename-${c.id}`);
    }, err => {
      this.renameProblem.set(reason(err) ?? 'The classification could not be renamed.');
      this.focus(`pcsRenameInput-${c.id}`);
    });
  }

  // ── Activate / deactivate ─────────────────────────────────────────────────

  toggleActive(c: ProjectClassification): void {
    if (this.busy()) return;
    const isActive = !c.isActive;
    this.save(c, { name: c.name, isActive },
      isActive ? `"${c.name}" is active again.` : `"${c.name}" is deactivated. Projects that have it keep it.`,
      () => this.focus(`pcsToggle-${c.id}`),
      err => {
        this.actionError.set(reason(err) ?? 'The classification could not be changed.');
        this.focus(`pcsToggle-${c.id}`);
      });
  }

  private save(
    c: ProjectClassification, input: { name: string; isActive: boolean }, done: string,
    onSuccess: () => void, onError: (err: HttpErrorResponse) => void,
  ): void {
    this.busy.set(true);
    this.actionError.set(null);
    this.service.update(this.workspaceId(), c.id, input).subscribe({
      next: res => {
        this.busy.set(false);
        if (res.data) this.items.update(list => list.map(x => (x.id === c.id ? res.data : x)));
        this.announce(done);
        onSuccess();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        onError(err);
      },
    });
  }

  // ── Reorder ───────────────────────────────────────────────────────────────

  move(c: ProjectClassification, direction: -1 | 1): void {
    if (this.busy()) return;
    const list = this.items();
    const from = list.findIndex(x => x.id === c.id);
    const to = from + direction;
    if (from < 0 || to < 0 || to >= list.length) return;

    const ids = list.map(x => x.id);
    [ids[from], ids[to]] = [ids[to], ids[from]];
    this.busy.set(true);
    this.actionError.set(null);
    this.service.reorder(this.workspaceId(), ids).subscribe({
      next: res => {
        this.busy.set(false);
        this.items.set(res.data ?? list);
        this.announce(`"${c.name}" moved to position ${to + 1} of ${list.length}.`);
        // Keep focus on the arrow that was used; at the end of the list use the other one.
        const atEdge = (direction === -1 && to === 0) || (direction === 1 && to === list.length - 1);
        const same = direction === -1 ? 'pcsUp' : 'pcsDown';
        const other = direction === -1 ? 'pcsDown' : 'pcsUp';
        this.focus(`${atEdge ? other : same}-${c.id}`);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.actionError.set(reason(err) ?? 'The order could not be saved.');
        this.refresh(() => this.focus(`pcsRename-${c.id}`));
      },
    });
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  askDelete(c: ProjectClassification): void {
    if (!c.canDelete || this.busy()) return;
    this.pendingDelete.set(c);
    this.actionError.set(null);
    this.deleteDialog()?.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    const c = this.pendingDelete();
    this.pendingDelete.set(null);
    if (!c) return;
    if (!confirmed) {
      this.focus(`pcsDelete-${c.id}`);
      return;
    }

    const list = this.items();
    const index = list.findIndex(x => x.id === c.id);
    this.busy.set(true);
    this.service.delete(this.workspaceId(), c.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.announce(`Deleted "${c.name}".`);
        this.refresh(() => {
          const next = this.items()[Math.min(index, this.items().length - 1)];
          this.focus(next ? `pcsRename-${next.id}` : 'pcsNewName', false, 'pcsHeading');
        });
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.actionError.set(reason(err) ?? 'The classification could not be deleted.');
        // The counts may have changed meanwhile (409): show the current state.
        this.refresh(() => this.focus(`pcsDelete-${c.id}`, false, `pcsRename-${c.id}`));
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private announce(text: string): void {
    this.announcement.set('');
    afterNextRender(() => this.announcement.set(text), { injector: this.injector });
  }

  /**
   * Moves focus once the view has rendered (the app coalesces change
   * detection, so a setTimeout could run first). Falls back to `fallbackId`
   * when the element is missing or disabled — for users without Edit the
   * buttons do not exist.
   */
  private focus(id: string, select = false, fallbackId?: string): void {
    afterNextRender(() => {
      const target = document.getElementById(id) as HTMLInputElement | null;
      const usable = target && !(target as HTMLButtonElement).disabled ? target : null;
      const element = usable ?? (fallbackId ? document.getElementById(fallbackId) : null);
      element?.focus();
      if (select && usable && typeof usable.select === 'function') usable.select();
    }, { injector: this.injector });
  }
}

/** The server's reason for a refused request (`ApiResponse.message`, else validation errors), or null. */
function reason(err: HttpErrorResponse | null | undefined): string | null {
  const body = err?.error;
  if (!body || typeof body !== 'object') return null;
  if (typeof body.message === 'string' && body.message.trim()) return body.message;
  if (Array.isArray(body.errors) && body.errors.length) return body.errors.join(' ');
  if (body.errors && typeof body.errors === 'object') {
    const first = Object.values(body.errors as Record<string, string[]>).flat()[0];
    if (typeof first === 'string') return first;
  }
  return null;
}
