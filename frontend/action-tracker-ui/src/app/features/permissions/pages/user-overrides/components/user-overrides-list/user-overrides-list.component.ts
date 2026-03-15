import { Component, DestroyRef, ViewChild, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UserPermissionOverrideDto } from '../../../../models/user-permission.model';
import { UserPermissionService } from '../../../../services/user-permission.service';
import { UserOverrideFormComponent } from '../user-override-form/user-override-form.component';
import { ConfirmDialogComponent } from '../../../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { HasPermissionDirective } from '../../../../../../shared/directives/has-permission.directive';

@Component({
  selector: 'app-user-overrides-list',
  standalone: true,
  imports: [CommonModule, UserOverrideFormComponent, ConfirmDialogComponent, HasPermissionDirective],
  templateUrl: './user-overrides-list.component.html',
  styleUrl: './user-overrides-list.component.scss',
})
export class UserOverridesListComponent {
  private readonly svc        = inject(UserPermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly userId          = input.required<string>();
  readonly userDisplayName = input.required<string>();

  /** Bubble up to page so the effective-permissions panel also refreshes. */
  readonly listChanged = output<void>();

  readonly overrides = signal<UserPermissionOverrideDto[]>([]);
  readonly loading   = signal(false);
  readonly error     = signal<string | null>(null);
  readonly deleting  = signal(false);

  private pendingDeleteId = '';

  @ViewChild('overrideForm') overrideForm!: UserOverrideFormComponent;
  @ViewChild('confirmDel')   confirmDel!: ConfirmDialogComponent;

  constructor() {
    effect(() => {
      const uid = this.userId();
      if (uid) this.load(uid);
    });
  }

  load(userId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.svc.getOverridesByUser(userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  (overrides) => { this.overrides.set(overrides); this.loading.set(false); },
        error: (err)       => { this.loading.set(false); this.error.set(err?.error?.message ?? 'Failed to load overrides.'); },
      });
  }

  openAddForm(): void {
    this.overrideForm.openCreate(this.userId(), this.userDisplayName());
  }

  openEditForm(override: UserPermissionOverrideDto): void {
    this.overrideForm.openEdit(override);
  }

  requestDelete(override: UserPermissionOverrideDto): void {
    this.pendingDeleteId = override.id;
    this.confirmDel.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    if (!confirmed) return;
    this.deleting.set(true);
    this.svc.deleteOverride(this.pendingDeleteId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.load(this.userId());
          this.listChanged.emit();
        },
        error: (err) => {
          this.deleting.set(false);
          this.error.set(err?.error?.message ?? 'Failed to delete override.');
        },
      });
  }

  onFormSaved(): void {
    this.load(this.userId());
    this.listChanged.emit();
  }
}
