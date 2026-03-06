import { Component, inject, signal, computed, HostListener } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';

interface NavLink {
  label: string;
  path: string;
  roles: string[] | null; // null = visible to all authenticated users
}

const NAV_LINKS: NavLink[] = [
  { label: 'Dashboard',       path: '/dashboard',   roles: null },
  { label: 'My Actions',      path: '/actions',     roles: null },
  { label: 'Reports',         path: '/reports',     roles: ['Admin', 'Manager'] },
  { label: 'Team Actions',    path: '/management',  roles: ['Admin', 'Manager'] },
  { label: 'Workspaces',      path: '/workspaces',  roles: ['Admin', 'Manager'] },
  { label: 'Admin Panel',     path: '/admin',       roles: ['Admin'] },
];

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, AsyncPipe],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  private readonly authService = inject(AuthService);

  /** Used by the template via the async pipe for reactive user display. */
  readonly currentUser$ = this.authService.currentUser$;

  /** Signal mirror — used by computed() for role-based link filtering. */
  private readonly currentUser = toSignal(this.authService.currentUser$, { initialValue: null });

  readonly menuOpen = signal(false);

  /** Nav links visible to the current user based on their roles. */
  readonly visibleLinks = computed(() => {
    const user = this.currentUser();
    if (!user) return [];
    return NAV_LINKS.filter(link =>
      link.roles === null || link.roles.some(r => user.roles.includes(r))
    );
  });

  toggleMenu(): void {
    this.menuOpen.update(v => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
  }

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: EventTarget | null): void {
    const el = target as HTMLElement | null;
    if (!el?.closest?.('.nav-user') && !el?.closest?.('.hamburger')) {
      this.menuOpen.set(false);
    }
  }
}
