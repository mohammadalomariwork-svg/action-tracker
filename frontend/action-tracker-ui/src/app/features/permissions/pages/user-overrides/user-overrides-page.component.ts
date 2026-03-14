import { Component, DestroyRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { UserEffectivePermissionsSummaryComponent } from './components/user-effective-permissions-summary/user-effective-permissions-summary.component';
import { UserOverridesListComponent } from './components/user-overrides-list/user-overrides-list.component';
import { UserManagementService } from '../../../user-management/services/user-management.service';
import { UserListItem } from '../../../user-management/models/user-management.models';

@Component({
  selector: 'app-user-overrides-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
    UserEffectivePermissionsSummaryComponent,
    UserOverridesListComponent,
  ],
  templateUrl: './user-overrides-page.component.html',
  styleUrl: './user-overrides-page.component.scss',
})
export class UserOverridesPageComponent implements OnInit {
  private readonly userMgmtService = inject(UserManagementService);
  private readonly destroyRef      = inject(DestroyRef);

  readonly searchControl    = new FormControl('', { nonNullable: true });
  readonly searchResults    = signal<UserListItem[]>([]);
  readonly searchLoading    = signal(false);
  readonly showDropdown     = signal(false);

  readonly selectedUserId   = signal<string | null>(null);
  readonly selectedUserName = signal<string>('');

  private readonly search$ = new Subject<string>();

  @ViewChild('overridesList')   overridesList!: UserOverridesListComponent;
  @ViewChild('permSummary')    permSummary!: UserEffectivePermissionsSummaryComponent;

  ngOnInit(): void {
    this.search$
      .pipe(
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((term) => {
          if (!term.trim()) {
            this.searchResults.set([]);
            this.showDropdown.set(false);
            this.searchLoading.set(false);
            return [];
          }
          this.searchLoading.set(true);
          return this.userMgmtService.getUsers(1, 10, term);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (result) => {
          this.searchResults.set(result.users);
          this.showDropdown.set(result.users.length > 0);
          this.searchLoading.set(false);
        },
        error: () => { this.searchLoading.set(false); },
      });
  }

  onSearchInput(value: string): void {
    this.search$.next(value);
  }

  selectUser(user: UserListItem): void {
    this.selectedUserId.set(user.id);
    this.selectedUserName.set(user.fullName);
    this.searchControl.setValue(user.fullName, { emitEvent: false });
    this.showDropdown.set(false);
    this.searchResults.set([]);
  }

  clearSelection(): void {
    this.selectedUserId.set(null);
    this.selectedUserName.set('');
    this.searchControl.setValue('');
    this.searchResults.set([]);
    this.showDropdown.set(false);
  }

  /** When an override is created/updated/deleted, also refresh the effective summary. */
  onListChanged(): void {
    this.permSummary?.refresh();
  }
}
