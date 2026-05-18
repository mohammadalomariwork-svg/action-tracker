import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, switchMap } from 'rxjs';

import { OrgUnitMember, OrgUnitService } from '../../services/org-unit.service';
import { UserManagementService } from '../../../user-management/services/user-management.service';
import { UserListItem } from '../../../user-management/models/user-management.models';
import { ToastService } from '../../../../core/services/toast.service';
import { OrgUnitTree } from '../../models/org-chart.models';

@Component({
  selector: 'app-org-unit-members',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './org-unit-members.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrgUnitMembersComponent implements OnChanges {
  private readonly orgUnitService = inject(OrgUnitService);
  private readonly userMgmt       = inject(UserManagementService);
  private readonly toast          = inject(ToastService);
  private readonly destroyRef     = inject(DestroyRef);

  @Input({ required: true }) unit!: OrgUnitTree;
  @Output() closed = new EventEmitter<void>();

  readonly members        = signal<OrgUnitMember[]>([]);
  readonly loading        = signal(false);
  readonly searchTerm     = signal('');
  readonly searchResults  = signal<UserListItem[]>([]);
  readonly searchLoading  = signal(false);
  readonly busyUserId     = signal<string | null>(null);

  private readonly search$ = new Subject<string>();

  readonly memberIds = computed(() => new Set(this.members().map(m => m.userId)));

  constructor() {
    this.search$
      .pipe(
        debounceTime(250),
        switchMap((term) => {
          const trimmed = term.trim();
          if (trimmed.length < 2) {
            this.searchLoading.set(false);
            return [{ users: [], totalCount: 0, page: 1, pageSize: 0 }];
          }
          this.searchLoading.set(true);
          return this.userMgmt.getUsers(1, 20, trimmed);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (res) => {
          this.searchResults.set(res.users.filter(u => u.isActive));
          this.searchLoading.set(false);
        },
        error: () => {
          this.searchResults.set([]);
          this.searchLoading.set(false);
        },
      });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['unit'] && this.unit) {
      this.loadMembers();
      this.searchTerm.set('');
      this.searchResults.set([]);
    }
  }

  private loadMembers(): void {
    this.loading.set(true);
    this.orgUnitService
      .getUsers(this.unit.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (m) => {
          this.members.set(m);
          this.loading.set(false);
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to load members.');
          this.loading.set(false);
        },
      });
  }

  onSearchInput(value: string): void {
    this.searchTerm.set(value);
    this.search$.next(value);
  }

  onAdd(user: UserListItem): void {
    if (this.memberIds().has(user.id)) return;

    if (user.orgUnitId && user.orgUnitId !== this.unit.id) {
      const ok = confirm(
        `${user.fullName} is currently assigned to "${user.orgUnitName ?? 'another unit'}". ` +
        `Move them to "${this.unit.name}"?`
      );
      if (!ok) return;
    }

    this.busyUserId.set(user.id);
    this.userMgmt
      .assignOrgUnit(user.id, { orgUnitId: this.unit.id })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`${user.fullName} assigned to ${this.unit.name}.`);
          this.busyUserId.set(null);
          this.loadMembers();
          this.onSearchInput(this.searchTerm());
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to assign user.');
          this.busyUserId.set(null);
        },
      });
  }

  onRemove(member: OrgUnitMember): void {
    if (!confirm(`Remove ${member.fullName} from ${this.unit.name}?`)) return;

    this.busyUserId.set(member.userId);
    this.userMgmt
      .assignOrgUnit(member.userId, { orgUnitId: null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success(`${member.fullName} removed from ${this.unit.name}.`);
          this.busyUserId.set(null);
          this.loadMembers();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? 'Failed to remove user.');
          this.busyUserId.set(null);
        },
      });
  }

  onClose(): void {
    this.closed.emit();
  }
}
