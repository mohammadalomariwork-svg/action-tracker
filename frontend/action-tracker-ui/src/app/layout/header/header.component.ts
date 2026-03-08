import { Component, inject, signal, computed, HostListener } from '@angular/core';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { AsyncPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';

interface NavLink {
  label: string;
  path: string;
  roles: string[] | null; // null = visible to all authenticated users
  /** Additional path prefixes that should highlight this nav link. */
  alsoActivePrefixes?: string[];
}

const NAV_LINKS: NavLink[] = [
  { label: 'Dashboard',       path: '/dashboard',   roles: null },
  { label: 'My Actions',      path: '/actions',     roles: null },
  { label: 'Reports',         path: '/reports',     roles: ['Admin', 'Manager'] },
  { label: 'Team Actions',    path: '/management',  roles: ['Admin', 'Manager'] },
  { label: 'Workspaces',      path: '/workspaces',  roles: ['Admin', 'Manager'], alsoActivePrefixes: ['/projects', '/action-items'] },
  { label: 'Admin Panel',     path: '/admin',       roles: ['Admin'] },
];

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, AsyncPipe],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  /** Used by the template via the async pipe for reactive user display. */
  readonly currentUser$ = this.authService.currentUser$;

  /** Signal mirror — used by computed() for role-based link filtering. */
  private readonly currentUser = toSignal(this.authService.currentUser$, { initialValue: null });

  /** Current URL path — updated on every navigation. */
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly menuOpen = signal(false);
  readonly navOpen = signal(false);

  /** Nav links visible to the current user based on their roles. */
  readonly visibleLinks = computed(() => {
    const user = this.currentUser();
    if (!user) return [];
    return NAV_LINKS.filter(link =>
      link.roles === null || link.roles.some(r => user.roles.includes(r))
    );
  });

  /** Check if a nav link should be highlighted based on current URL. */
  isLinkActive(link: NavLink): boolean {
    const url = this.currentUrl();
    if (link.path === '/dashboard') return url === '/dashboard';
    if (url.startsWith(link.path)) return true;
    return link.alsoActivePrefixes?.some(prefix => url.startsWith(prefix)) ?? false;
  }

  toggleMenu(): void {
    this.menuOpen.update(v => !v);
  }

  toggleNav(): void {
    this.navOpen.update(v => !v);
    this.menuOpen.set(false); // close user dropdown when toggling nav
  }

  closeMenu(): void {
    this.menuOpen.set(false);
    this.navOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
  }

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: EventTarget | null): void {
    const el = target as HTMLElement | null;
    if (!el?.closest?.('.nav-user') && !el?.closest?.('.hamburger')) {
      this.menuOpen.set(false);
      this.navOpen.set(false);
    }
  }
}
