import { Component, DestroyRef, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { WorkspaceService } from '../../services/workspace.service';
import { AuthService } from '../../../../core/services/auth.service';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import {
  Workspace, WorkspaceAdmin, WorkspaceMember,
  WorkspaceProjectManager, WorkspaceStrategicProjectApprover,
  UserDropdownItem, OrgUnitDropdownItem, StrategicPlanDropdownItem,
} from '../../models/workspace.model';

type AddResult<T> = { failed?: boolean; data?: T; user: UserDropdownItem; err?: any };

@Component({
  selector: 'app-workspace-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, BreadcrumbComponent],
  templateUrl: './workspace-settings.component.html',
  styleUrl: './workspace-settings.component.scss',
})
export class WorkspaceSettingsComponent implements OnInit {
  private readonly workspaceService = inject(WorkspaceService);
  private readonly authService      = inject(AuthService);
  private readonly route            = inject(ActivatedRoute);
  private readonly router           = inject(Router);
  private readonly destroyRef       = inject(DestroyRef);

  workspaceId!: string;
  workspace: Workspace | null = null;
  isLoading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;
  canManage = false;

  // Shared user catalog
  private allActiveUsers: UserDropdownItem[] = [];

  // ── Linked strategic plan ───────────────────────────────────
  strategicPlanOptions: StrategicPlanDropdownItem[] = [];
  selectedStrategicPlanId = '';
  isSavingStrategy = false;

  // ── Admins ──────────────────────────────────────────────────
  availableAdmins: UserDropdownItem[] = [];
  selectedAdminUserIds: string[] = [];
  adminDropdownOpen = false;
  adminSearchTerm = '';
  isAddingAdmin = false;

  // ── Members ─────────────────────────────────────────────────
  availableMembers: UserDropdownItem[] = [];
  selectedMemberUserIds: string[] = [];
  memberDropdownOpen = false;
  memberSearchTerm = '';
  isAddingMember = false;

  // ── Bulk add members from org unit ──────────────────────────
  orgUnitOptions: OrgUnitDropdownItem[] = [];
  selectedOrgUnitForBulk = '';
  isAddingFromOrgUnit = false;

  /**
   * Builds the visible text for an org-unit option, indenting children under
   * their parent so the dropdown reflects the hierarchy. The backend already
   * returns the list in depth-first order, so we only need to apply visual
   * indentation based on `level`.
   */
  orgUnitOptionLabel(o: OrgUnitDropdownItem): string {
    const indent = '    '.repeat(Math.max(0, o.level - 1));
    const prefix = o.level > 1 ? '└─ ' : '';
    const code   = o.code ? ` (${o.code})` : '';
    return `${indent}${prefix}${o.name}${code}`;
  }

  // ── Project Managers ────────────────────────────────────────
  availablePms: UserDropdownItem[] = [];
  selectedPmUserIds: string[] = [];
  pmDropdownOpen = false;
  pmSearchTerm = '';
  isAddingPm = false;

  // ── Strategic Project Approvers (ordered) ───────────────────
  availableApprovers: UserDropdownItem[] = [];
  selectedApproverUserIds: string[] = [];
  approverDropdownOpen = false;
  approverSearchTerm = '';
  isAddingApprover = false;
  isReorderingApprovers = false;

  ngOnInit(): void {
    this.workspaceId = this.route.snapshot.paramMap.get('id')!;
    this.loadData();
  }

  // ── Data loading ────────────────────────────────────────────
  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.workspaceService.getWorkspaceById(this.workspaceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.workspace = res.data ?? null;
          this.selectedStrategicPlanId = this.workspace?.strategicPlanId ?? '';
          this.resolvePermissions();
          this.isLoading = false;
          if (this.canManage) {
            this.loadActiveUsers();
            this.loadStrategicPlans();
            this.loadOrgUnitsForBulk();
          }
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load workspace.';
          this.isLoading = false;
        },
      });
  }

  private resolvePermissions(): void {
    if (this.authService.hasRole('Admin') || this.authService.hasRole('Manager')) {
      this.canManage = true;
      return;
    }
    this.authService.currentUser$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(user => {
        if (user && this.workspace?.admins?.some(a => a.userId === user.email)) {
          this.canManage = true;
        }
      });
  }

  private loadActiveUsers(): void {
    this.workspaceService.getActiveUsersForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.allActiveUsers = res.data ?? [];
          this.recomputeAvailable();
        },
        error: () => {},
      });
  }

  private loadStrategicPlans(): void {
    this.workspaceService.getStrategicPlansForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.strategicPlanOptions = res.data ?? [],
        error: () => {},
      });
  }

  private loadOrgUnitsForBulk(): void {
    this.workspaceService.getOrgUnitsForDropdown()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.orgUnitOptions = res.data ?? [],
        error: () => {},
      });
  }

  onAddMembersFromOrgUnit(): void {
    if (!this.workspace || !this.selectedOrgUnitForBulk) return;

    const orgUnitId = this.selectedOrgUnitForBulk;
    const unitName  = this.orgUnitOptions.find(o => o.id === orgUnitId)?.name ?? 'org unit';

    this.isAddingFromOrgUnit = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.workspaceService.addMembersFromOrgUnit(this.workspaceId, orgUnitId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const result = res.data;
          if (result && this.workspace) {
            this.workspace = {
              ...this.workspace,
              members: [...(this.workspace.members ?? []), ...(result.added ?? [])],
            };
            this.recomputeAvailable();
          }
          this.isAddingFromOrgUnit = false;
          this.selectedOrgUnitForBulk = '';

          if (!result || result.totalUsersInOrgUnit === 0) {
            // Empty unit — render in the error banner so it's visually distinct
            // (assigning users to the org unit is a prerequisite the admin must
            // satisfy via the Organization Chart page).
            this.errorMessage =
              `No active users are assigned to the org unit "${unitName}". ` +
              `Assign users to this org unit first via the Organization Chart admin page.`;
          } else if (result.added.length === 0) {
            this.successMessage =
              `All ${result.totalUsersInOrgUnit} active user(s) in "${unitName}" ` +
              `are already workspace members — no new members were added.`;
          } else {
            const memberWord = result.added.length === 1 ? 'member' : 'members';
            const skipped = result.skippedAlreadyMembers > 0
              ? ` (${result.skippedAlreadyMembers} already-member(s) skipped, no duplicates created)`
              : '';
            this.successMessage =
              `Added ${result.added.length} new ${memberWord} from "${unitName}".${skipped}`;
          }
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to add members from org unit.';
          this.isAddingFromOrgUnit = false;
        },
      });
  }

  private recomputeAvailable(): void {
    if (!this.workspace) return;

    const adminIds = new Set(this.workspace.admins?.map(a => a.userId) ?? []);
    const memberIds = new Set(this.workspace.members?.map(m => m.userId) ?? []);
    const pmIds = new Set(this.workspace.projectManagers?.map(p => p.userId) ?? []);
    const approverIds = new Set(this.workspace.strategicProjectApprovers?.map(a => a.userId) ?? []);

    this.availableAdmins   = this.allActiveUsers.filter(u => !adminIds.has(u.id));
    this.availableMembers  = this.allActiveUsers.filter(u => !memberIds.has(u.id) && !adminIds.has(u.id));
    this.availablePms      = this.allActiveUsers.filter(u => !pmIds.has(u.id));
    this.availableApprovers = this.allActiveUsers.filter(u => !approverIds.has(u.id));
  }

  // ── Dropdown outside-click handling ─────────────────────────
  @HostListener('document:click')
  onDocumentClick(): void {
    this.adminDropdownOpen = false;
    this.memberDropdownOpen = false;
    this.pmDropdownOpen = false;
    this.approverDropdownOpen = false;
  }

  // ── Search helpers (shared template logic) ──────────────────
  filterUsers(source: UserDropdownItem[], term: string): UserDropdownItem[] {
    const q = term.trim().toLowerCase();
    if (!q) return source;
    return source.filter(u =>
      (u.displayName ?? '').toLowerCase().includes(q) ||
      (u.email ?? '').toLowerCase().includes(q) ||
      (u.orgUnitName ?? '').toLowerCase().includes(q)
    );
  }

  get filteredAdmins(): UserDropdownItem[] { return this.filterUsers(this.availableAdmins, this.adminSearchTerm); }
  get filteredMembers(): UserDropdownItem[] { return this.filterUsers(this.availableMembers, this.memberSearchTerm); }
  get filteredPms(): UserDropdownItem[] { return this.filterUsers(this.availablePms, this.pmSearchTerm); }
  get filteredApprovers(): UserDropdownItem[] { return this.filterUsers(this.availableApprovers, this.approverSearchTerm); }

  // ── Toggle helpers ──────────────────────────────────────────
  toggle(list: string[], id: string): void {
    const idx = list.indexOf(id);
    if (idx >= 0) list.splice(idx, 1);
    else list.push(id);
  }

  // Display-name lookups used in the chip rendering
  getUserDisplayName(userId: string): string {
    return this.allActiveUsers.find(u => u.id === userId)?.displayName ?? userId;
  }

  // ── Add operations (use parallel POSTs; mirror member pattern) ──────────
  onAddAdmins(): void {
    if (!this.workspace || this.selectedAdminUserIds.length === 0) return;
    const users = this.usersFromIds(this.selectedAdminUserIds, this.availableAdmins);
    if (users.length === 0) return;

    this.isAddingAdmin = true;
    this.errorMessage = null;
    this.successMessage = null;

    // Admins are still managed via PUT /workspaces/{id} (full admin list replacement).
    const updatedAdmins: WorkspaceAdmin[] = [
      ...(this.workspace.admins ?? []),
      ...users.map(u => ({ userId: u.id, userName: u.displayName, email: '', orgUnitName: '' })),
    ];

    this.workspaceService.updateWorkspace(this.workspaceId, {
      id: this.workspaceId,
      title: this.workspace.title,
      organizationUnit: this.workspace.organizationUnit,
      orgUnitId: this.workspace.orgUnitId,
      strategicPlanId: this.workspace.strategicPlanId,
      admins: updatedAdmins,
      isActive: this.workspace.isActive,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.data) this.workspace = this.mergeWorkspace(res.data);
          this.selectedAdminUserIds = [];
          this.adminSearchTerm = '';
          this.adminDropdownOpen = false;
          this.recomputeAvailable();
          this.successMessage = users.length === 1
            ? `${users[0].displayName} added as admin.`
            : `${users.length} admins added.`;
          this.isAddingAdmin = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to add admin.';
          this.isAddingAdmin = false;
        },
      });
  }

  onAddMembers(): void {
    if (!this.workspace || this.selectedMemberUserIds.length === 0) return;
    const users = this.usersFromIds(this.selectedMemberUserIds, this.availableMembers);
    if (users.length === 0) return;

    this.runParallelAdds(
      users,
      (u) => this.workspaceService.addMember(this.workspaceId, { userId: u.id, userName: u.displayName }),
      (added) => {
        if (this.workspace) {
          this.workspace = {
            ...this.workspace,
            members: [...(this.workspace.members ?? []), ...(added as WorkspaceMember[])],
          };
        }
      },
      () => { this.selectedMemberUserIds = []; this.memberSearchTerm = ''; this.memberDropdownOpen = false; },
      (flag) => this.isAddingMember = flag,
      'member',
    );
  }

  onAddProjectManagers(): void {
    if (!this.workspace || this.selectedPmUserIds.length === 0) return;
    const users = this.usersFromIds(this.selectedPmUserIds, this.availablePms);
    if (users.length === 0) return;

    this.runParallelAdds(
      users,
      (u) => this.workspaceService.addProjectManager(this.workspaceId, { userId: u.id, userName: u.displayName }),
      (added) => {
        if (this.workspace) {
          this.workspace = {
            ...this.workspace,
            projectManagers: [...(this.workspace.projectManagers ?? []), ...(added as WorkspaceProjectManager[])],
          };
        }
      },
      () => { this.selectedPmUserIds = []; this.pmSearchTerm = ''; this.pmDropdownOpen = false; },
      (flag) => this.isAddingPm = flag,
      'project manager',
    );
  }

  onAddStrategicApprovers(): void {
    if (!this.workspace || this.selectedApproverUserIds.length === 0) return;
    // For approvers, sequential add (one at a time) is important so the Order
    // increments correctly on the server side.
    const users = this.usersFromIds(this.selectedApproverUserIds, this.availableApprovers);
    if (users.length === 0) return;

    this.isAddingApprover = true;
    this.errorMessage = null;
    this.successMessage = null;

    const addNext = (i: number, added: WorkspaceStrategicProjectApprover[]): void => {
      if (i >= users.length) {
        if (this.workspace) {
          this.workspace = {
            ...this.workspace,
            strategicProjectApprovers: [...(this.workspace.strategicProjectApprovers ?? []), ...added],
          };
        }
        this.selectedApproverUserIds = [];
        this.approverSearchTerm = '';
        this.approverDropdownOpen = false;
        this.recomputeAvailable();
        this.isAddingApprover = false;
        this.successMessage = added.length === 1
          ? `${added[0].userName} added to the approver chain.`
          : `${added.length} approvers added to the chain.`;
        return;
      }
      const u = users[i];
      this.workspaceService.addStrategicProjectApprover(this.workspaceId, { userId: u.id, userName: u.displayName })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (res) => {
            if (res.data) added.push(res.data);
            addNext(i + 1, added);
          },
          error: (err) => {
            this.errorMessage = err?.error?.message ?? `Failed to add ${u.displayName}.`;
            this.isAddingApprover = false;
            // Commit whatever we got so far
            if (this.workspace && added.length > 0) {
              this.workspace = {
                ...this.workspace,
                strategicProjectApprovers: [...(this.workspace.strategicProjectApprovers ?? []), ...added],
              };
              this.recomputeAvailable();
            }
          },
        });
    };

    addNext(0, []);
  }

  // ── Remove operations ───────────────────────────────────────
  onRemoveAdmin(admin: WorkspaceAdmin): void {
    if (!this.workspace) return;
    if ((this.workspace.admins?.length ?? 0) <= 1) {
      this.errorMessage = 'A workspace must have at least one admin.';
      return;
    }
    if (!confirm(`Remove ${admin.userName} as admin?`)) return;

    this.errorMessage = null;
    this.successMessage = null;
    const updated = this.workspace.admins.filter(a => a.userId !== admin.userId);

    this.workspaceService.updateWorkspace(this.workspaceId, {
      id: this.workspaceId,
      title: this.workspace.title,
      organizationUnit: this.workspace.organizationUnit,
      orgUnitId: this.workspace.orgUnitId,
      strategicPlanId: this.workspace.strategicPlanId,
      admins: updated,
      isActive: this.workspace.isActive,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.data) this.workspace = this.mergeWorkspace(res.data);
          this.recomputeAvailable();
          this.successMessage = `${admin.userName} removed from admins.`;
        },
        error: (err) => this.errorMessage = err?.error?.message ?? 'Failed to remove admin.',
      });
  }

  onRemoveMember(member: WorkspaceMember): void {
    if (!confirm(`Remove ${member.userName} from members?`)) return;
    this.workspaceService.removeMember(this.workspaceId, member.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.workspace) {
            this.workspace = {
              ...this.workspace,
              members: this.workspace.members.filter(m => m.userId !== member.userId),
            };
          }
          this.recomputeAvailable();
          this.successMessage = `${member.userName} removed from members.`;
        },
        error: (err) => this.errorMessage = err?.error?.message ?? 'Failed to remove member.',
      });
  }

  onRemoveProjectManager(pm: WorkspaceProjectManager): void {
    if (!confirm(`Remove ${pm.userName} from project managers?`)) return;
    this.workspaceService.removeProjectManager(this.workspaceId, pm.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.workspace) {
            this.workspace = {
              ...this.workspace,
              projectManagers: this.workspace.projectManagers.filter(p => p.userId !== pm.userId),
            };
          }
          this.recomputeAvailable();
          this.successMessage = `${pm.userName} removed from project managers.`;
        },
        error: (err) => this.errorMessage = err?.error?.message ?? 'Failed to remove project manager.',
      });
  }

  onRemoveStrategicApprover(approver: WorkspaceStrategicProjectApprover): void {
    if (!confirm(`Remove ${approver.userName} from the strategic-project approver chain?`)) return;
    this.workspaceService.removeStrategicProjectApprover(this.workspaceId, approver.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.workspace) {
            // Re-densify Order locally to match server behaviour
            const remaining = (this.workspace.strategicProjectApprovers ?? [])
              .filter(a => a.userId !== approver.userId)
              .sort((a, b) => a.order - b.order)
              .map((a, i) => ({ ...a, order: i + 1 }));
            this.workspace = { ...this.workspace, strategicProjectApprovers: remaining };
          }
          this.recomputeAvailable();
          this.successMessage = `${approver.userName} removed from the approver chain.`;
        },
        error: (err) => this.errorMessage = err?.error?.message ?? 'Failed to remove approver.',
      });
  }

  // ── Approver chain reorder ──────────────────────────────────
  moveApproverUp(index: number): void {
    if (!this.workspace) return;
    const list = [...(this.workspace.strategicProjectApprovers ?? [])].sort((a, b) => a.order - b.order);
    if (index <= 0 || index >= list.length) return;
    [list[index - 1], list[index]] = [list[index], list[index - 1]];
    this.persistApproverOrder(list);
  }

  moveApproverDown(index: number): void {
    if (!this.workspace) return;
    const list = [...(this.workspace.strategicProjectApprovers ?? [])].sort((a, b) => a.order - b.order);
    if (index < 0 || index >= list.length - 1) return;
    [list[index], list[index + 1]] = [list[index + 1], list[index]];
    this.persistApproverOrder(list);
  }

  private persistApproverOrder(orderedList: WorkspaceStrategicProjectApprover[]): void {
    if (!this.workspace) return;
    this.isReorderingApprovers = true;
    this.errorMessage = null;
    this.successMessage = null;

    this.workspaceService.reorderStrategicProjectApprovers(
      this.workspaceId, { userIds: orderedList.map(a => a.userId) })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.workspace) {
            this.workspace = {
              ...this.workspace,
              strategicProjectApprovers: orderedList.map((a, i) => ({ ...a, order: i + 1 })),
            };
          }
          this.successMessage = 'Approver chain reordered.';
          this.isReorderingApprovers = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to reorder approvers.';
          this.isReorderingApprovers = false;
        },
      });
  }

  // ── Strategy linking ────────────────────────────────────────
  onSaveStrategy(): void {
    if (!this.workspace) return;
    this.isSavingStrategy = true;
    this.errorMessage = null;
    this.successMessage = null;

    const planId = this.selectedStrategicPlanId || null;
    this.workspaceService.setStrategy(this.workspaceId, { strategicPlanId: planId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.data) {
            this.workspace = this.mergeWorkspace(res.data);
            this.selectedStrategicPlanId = res.data.strategicPlanId ?? '';
          }
          this.successMessage = planId ? 'Strategy linked.' : 'Strategy cleared.';
          this.isSavingStrategy = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to update strategy.';
          this.isSavingStrategy = false;
        },
      });
  }

  // ── Back to detail ──────────────────────────────────────────
  goBackToDetail(): void {
    this.router.navigate(['/workspaces', this.workspaceId]);
  }

  // ── Local helpers ───────────────────────────────────────────
  private usersFromIds(ids: string[], pool: UserDropdownItem[]): UserDropdownItem[] {
    return ids
      .map(id => pool.find(u => u.id === id))
      .filter((u): u is UserDropdownItem => !!u);
  }

  /** Keep a server response without erasing fields the server didn't return. */
  private mergeWorkspace(updated: Workspace): Workspace {
    if (!this.workspace) return updated;
    return {
      ...this.workspace,
      ...updated,
      // Preserve collections that may not have come back from a focused endpoint
      members: updated.members ?? this.workspace.members,
      projectManagers: updated.projectManagers ?? this.workspace.projectManagers,
      strategicProjectApprovers: updated.strategicProjectApprovers ?? this.workspace.strategicProjectApprovers,
    };
  }

  private runParallelAdds<T>(
    users: UserDropdownItem[],
    request: (u: UserDropdownItem) => any,
    onAddedAll: (added: T[]) => void,
    resetSelection: () => void,
    setBusy: (flag: boolean) => void,
    label: string,
  ): void {
    setBusy(true);
    this.errorMessage = null;
    this.successMessage = null;

    const obs = users.map(u =>
      request(u).pipe(catchError((err: any) => of({ failed: true, user: u, err } as AddResult<T>)))
    );

    forkJoin(obs)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (results: any[]) => {
          const added: T[] = [];
          const failed: { user: UserDropdownItem; err: any }[] = [];
          results.forEach((r) => {
            if (r && r.failed) failed.push({ user: r.user, err: r.err });
            else if (r?.data) added.push(r.data as T);
          });

          onAddedAll(added);
          resetSelection();
          this.recomputeAvailable();
          setBusy(false);

          if (failed.length === 0) {
            this.successMessage = added.length === 1
              ? `1 ${label} added.`
              : `${added.length} ${label}s added.`;
          } else if (added.length === 0) {
            this.errorMessage = failed[0].err?.error?.message ?? `Failed to add ${label}s.`;
          } else {
            this.successMessage = `${added.length} ${label}(s) added.`;
            this.errorMessage = `Failed to add: ${failed.map(f => f.user.displayName).join(', ')}.`;
          }
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? `Failed to add ${label}s.`;
          setBusy(false);
        },
      });
  }
}
