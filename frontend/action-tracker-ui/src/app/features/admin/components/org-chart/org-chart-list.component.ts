import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { OrgUnitService } from '../../services/org-unit.service';
import { ToastService } from '../../../../core/services/toast.service';
import { OrgUnit, OrgUnitTree } from '../../models/org-chart.models';
import { OrgUnitFormComponent } from './org-unit-form.component';

@Component({
  selector: 'app-org-chart-list',
  standalone: true,
  imports: [CommonModule, FormsModule, NgTemplateOutlet, OrgUnitFormComponent],
  templateUrl: './org-chart-list.component.html',
  styleUrl: './org-chart-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrgChartListComponent implements OnInit {
  private readonly orgUnitService = inject(OrgUnitService);
  private readonly toast          = inject(ToastService);
  private readonly destroyRef     = inject(DestroyRef);

  // ── Signals ─────────────────────────────────────────────────────────────────
  readonly tree         = signal<OrgUnitTree | null>(null);
  readonly loading      = signal(false);
  readonly selectedNode = signal<OrgUnitTree | null>(null);
  readonly mode         = signal<'add' | 'edit' | null>(null);
  readonly parentForAdd = signal<OrgUnitTree | null>(null);
  readonly showDeleted  = signal(false);

  /** Set of node IDs that are collapsed. All expanded by default. */
  readonly collapsedIds = signal<Set<string>>(new Set());

  ngOnInit(): void {
    this.loadTree();
  }

  // ── Tree loading ─────────────────────────────────────────────────────────────

  loadTree(): void {
    this.loading.set(true);
    this.orgUnitService
      .getTree(this.showDeleted())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (t) => {
          this.tree.set(t);
          this.loading.set(false);
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to load org chart.');
          this.loading.set(false);
        },
      });
  }

  onShowDeletedChange(value: boolean): void {
    this.showDeleted.set(value);
    this.loadTree();
  }

  // ── Expand / collapse ────────────────────────────────────────────────────────

  isCollapsed(id: string): boolean {
    return this.collapsedIds().has(id);
  }

  toggleCollapse(id: string): void {
    const s = new Set(this.collapsedIds());
    s.has(id) ? s.delete(id) : s.add(id);
    this.collapsedIds.set(s);
  }

  expandAll(): void {
    this.collapsedIds.set(new Set());
  }

  collapseAll(): void {
    const ids = this.collectAllIds(this.tree());
    this.collapsedIds.set(new Set(ids));
  }

  private collectAllIds(node: OrgUnitTree | null): string[] {
    if (!node) return [];
    return [node.id, ...node.children.flatMap((c) => this.collectAllIds(c))];
  }

  // ── Node actions ─────────────────────────────────────────────────────────────

  onAddChild(node: OrgUnitTree): void {
    this.parentForAdd.set(node);
    this.selectedNode.set(null);
    this.mode.set('add');
  }

  onEdit(node: OrgUnitTree): void {
    this.selectedNode.set(node);
    this.parentForAdd.set(null);
    this.mode.set('edit');
  }

  onDelete(node: OrgUnitTree): void {
    if (!confirm(`Delete "${node.name}" and all its children?`)) return;

    this.orgUnitService
      .softDelete(node.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`"${node.name}" deleted.`);
          if (this.selectedNode()?.id === node.id) this.closeForm();
          this.loadTree();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to delete.'),
      });
  }

  onRestore(node: OrgUnitTree): void {
    this.orgUnitService
      .restore(node.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`"${node.name}" restored.`);
          this.loadTree();
        },
        error: (err) => this.toast.error(err?.error?.message ?? 'Failed to restore.'),
      });
  }

  // ── Form panel ───────────────────────────────────────────────────────────────

  onFormSaved(_unit: OrgUnit): void {
    this.toast.success(this.mode() === 'add' ? 'Org unit created.' : 'Org unit updated.');
    this.closeForm();
    this.loadTree();
  }

  onFormCancelled(): void {
    this.closeForm();
  }

  closeForm(): void {
    this.mode.set(null);
    this.selectedNode.set(null);
    this.parentForAdd.set(null);
  }

  /** Returns the parent OrgUnitTree node for a given node by searching the tree. */
  getParentNode(node: OrgUnitTree): OrgUnitTree | null {
    if (!this.tree()) return null;
    return this.findParent(this.tree()!, node.id);
  }

  private findParent(current: OrgUnitTree, targetId: string): OrgUnitTree | null {
    for (const child of current.children) {
      if (child.id === targetId) return current;
      const found = this.findParent(child, targetId);
      if (found) return found;
    }
    return null;
  }
}
