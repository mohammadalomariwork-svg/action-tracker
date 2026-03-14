import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EffectivePermissionDto } from '../../../../models/user-permission.model';
import { UserPermissionService } from '../../../../services/user-permission.service';

@Component({
  selector: 'app-user-effective-permissions-summary',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-effective-permissions-summary.component.html',
  styleUrl: './user-effective-permissions-summary.component.scss',
})
export class UserEffectivePermissionsSummaryComponent {
  private readonly svc        = inject(UserPermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly userId = input.required<string>();

  readonly permissions = signal<EffectivePermissionDto[]>([]);
  readonly loading     = signal(false);
  readonly error       = signal<string | null>(null);

  constructor() {
    effect(() => {
      const uid = this.userId();
      if (uid) this.load(uid);
    });
  }

  refresh(): void {
    const uid = this.userId();
    if (uid) this.load(uid);
  }

  private load(userId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.svc.getEffectivePermissions(userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next:  (perms) => { this.permissions.set(perms); this.loading.set(false); },
        error: (err)   => { this.loading.set(false); this.error.set(err?.error?.message ?? 'Failed to load effective permissions.'); },
      });
  }

  sourceLabel(source: string): string {
    if (source === 'Role')                  return 'Role';
    if (source === 'UserOverride-Granted')  return 'Override (Granted)';
    if (source === 'UserOverride-Revoked')  return 'Override (Revoked)';
    return source;
  }

  sourceBadgeClass(source: string): string {
    if (source === 'Role')                  return 'badge bg-secondary';
    if (source === 'UserOverride-Granted')  return 'badge bg-success';
    if (source === 'UserOverride-Revoked')  return 'badge bg-danger';
    return 'badge bg-light text-dark';
  }
}
