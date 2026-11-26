import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnInit,
  Output,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { StrategicPlanWorkflowService } from '../../../../services/strategic-plan-workflow.service';
import { StrategicPlanApprover } from '../../../../models/strategic-plan-workflow.model';
import { WorkspaceService } from '../../../workspaces/services/workspace.service';
import { UserDropdownItem } from '../../../workspaces/models/workspace.model';
import { showErrorHere } from '../../../../core/http/server-reason';

/**
 * Manage Approvers offcanvas — replaces the plan's approval chain wholesale.
 * Visible only on Draft plans (the parent gates this; the backend rejects
 * upserts on any other status). The chain is ordered top-to-bottom = level 1
 * upward; reorder with the up/down arrows; remove with the X.
 *
 * Cancel emits without saving. Save calls `PUT /api/strategic-plans/:id/approvers`
 * and emits the persisted list on success — server-side normalises gaps and
 * deduplicates, so what comes back is the source of truth.
 */
@Component({
  selector: 'app-manage-approvers-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manage-approvers-dialog.component.html',
  styleUrl: './manage-approvers-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageApproversDialogComponent implements OnInit {
  @Input() planId!: string;
  @Input() set existingApprovers(value: StrategicPlanApprover[]) {
    // Defensive copy — never mutate the input array; the parent owns it.
    this.chain.set([...(value ?? [])].sort((a, b) => a.order - b.order));
  }

  @Output() cancel = new EventEmitter<void>();
  @Output() saved  = new EventEmitter<StrategicPlanApprover[]>();

  private readonly workflow = inject(StrategicPlanWorkflowService);
  private readonly workspaceSvc = inject(WorkspaceService);
  private readonly destroyRef = inject(DestroyRef);

  readonly chain = signal<StrategicPlanApprover[]>([]);
  readonly allUsers = signal<UserDropdownItem[]>([]);
  readonly searchTerm = signal('');
  readonly pickerOpen = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly canSave = computed(() => this.chain().length > 0);

  /** Active users not yet in the chain, filtered by the search term. */
  readonly availableUsers = computed(() => {
    const inChain = new Set(this.chain().map((a) => a.userId));
    const term = this.searchTerm().trim().toLowerCase();
    const list = this.allUsers().filter((u) => !inChain.has(u.id));
    if (!term) return list;
    return list.filter(
      (u) =>
        u.displayName.toLowerCase().includes(term) ||
        (u.email?.toLowerCase().includes(term) ?? false) ||
        (u.orgUnitName?.toLowerCase().includes(term) ?? false),
    );
  });

  ngOnInit(): void {
    this.workspaceSvc
      .getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success) this.allUsers.set(res.data ?? []);
        },
        error: () => this.error.set('Failed to load users.'),
      });
  }

  togglePicker(): void {
    this.pickerOpen.set(!this.pickerOpen());
  }

  addApprover(user: UserDropdownItem): void {
    const next = [...this.chain()];
    next.push({
      id: '',                  // server assigns on save
      strategicPlanId: this.planId,
      userId: user.id,
      displayName: user.displayName,
      email: user.email,
      orgUnitName: user.orgUnitName,
      order: next.length + 1,
    });
    this.chain.set(this.renumber(next));
    this.searchTerm.set('');
    this.pickerOpen.set(false);
  }

  removeAt(index: number): void {
    const next = this.chain().filter((_, i) => i !== index);
    this.chain.set(this.renumber(next));
  }

  moveUp(index: number): void {
    if (index <= 0) return;
    const next = [...this.chain()];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.chain.set(this.renumber(next));
  }

  moveDown(index: number): void {
    const arr = this.chain();
    if (index >= arr.length - 1) return;
    const next = [...arr];
    [next[index + 1], next[index]] = [next[index], next[index + 1]];
    this.chain.set(this.renumber(next));
  }

  save(): void {
    if (!this.canSave()) return;
    this.submitting.set(true);
    this.error.set(null);

    const payload = {
      approvers: this.chain().map((a, i) => ({ userId: a.userId, order: i + 1 })),
    };

    this.workflow
      .upsertApprovers(this.planId, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          if (res.success) {
            this.saved.emit(res.data ?? []);
          } else {
            this.error.set(res.message ?? 'Failed to save approvers.');
          }
        },
        error: (err) => {
          this.submitting.set(false);
          this.error.set(showErrorHere(err) ?? this.extractError(err));
        },
      });
  }

  /**
   * Pulls the most informative error string from an HttpErrorResponse, in
   * order: ApiResponse `{success:false, message}`, RFC 7807 ProblemDetails
   * (`detail` / `title` — used by the global ExceptionMiddleware for
   * unhandled exceptions like `SqlException`), Angular's own statusText, and
   * finally a generic fallback that includes the status code so the user can
   * tell a 401 apart from a 500.
   */
  private extractError(err: any): string {
    const body = err?.error;
    if (typeof body === 'string' && body.length > 0) return body;
    if (body?.message) return body.message;
    if (body?.detail)  return body.detail;
    if (body?.title)   return body.title;
    if (err?.message)  return err.message;
    if (err?.status)   return `Failed to save approvers (HTTP ${err.status}).`;
    return 'Failed to save approvers.';
  }

  onCancel(): void {
    this.cancel.emit();
  }

  /** Reassigns dense 1-based `order` values after any list change. */
  private renumber(arr: StrategicPlanApprover[]): StrategicPlanApprover[] {
    return arr.map((a, i) => ({ ...a, order: i + 1 }));
  }
}
