import { Component, inject, signal, computed, HostListener } from '@angular/core';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { AsyncPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';
import { EffectivePermissionDto } from '../../features/permissions/models/user-permission.model';

interface NavLink {
  label: string;
  path: string;
  /**
   * Visibility rule:
   *  - null              → always visible to authenticated users
   *  - { area, action }  → visible when the user holds that effective permission
   *  - { role }          → visible when the user has that Identity role (admin-only gates)
   */
  visibleWhen: null | { area: string; action: string } | { role: string };
  /** Additional path prefixes that should highlight this nav link. */
  alsoActivePrefixes?: string[];
}

const NAV_LINKS: NavLink[] = [
  {
    label: 'Dashboards',
    path: '/dashboard',
    visibleWhen: { area: 'Dashboard', action: 'View' },
  },
  {
    label: 'My Actions',
    path: '/actions',
    visibleWhen: null,
  },
  {
    label: 'Reports',
    path: '/reports',
    visibleWhen: { area: 'Reports', action: 'View' },
  },
  {
    label: 'Team Actions',
    path: '/management',
    visibleWhen: { area: 'Action Items', action: 'View' },
    alsoActivePrefixes: ['/projects', '/action-items'],
  },
  {
    label: 'Workspaces',
    path: '/workspaces',
    visibleWhen: { area: 'Workspaces', action: 'View' },
  },
  {
    label: 'Admin Panel',
    path: '/admin',
    visibleWhen: { role: 'Admin' },
  },
];

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, AsyncPipe],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  private readonly authService       = inject(AuthService);
  private readonly permissionState   = inject(PermissionStateService);
  private readonly router            = inject(Router);

  /** Used by the template via the async pipe for reactive user display. */
  readonly currentUser$ = this.authService.currentUser$;

  /** Signal mirror — used by computed() for role-based link filtering. */
  private readonly currentUser = toSignal(this.authService.currentUser$, { initialValue: null });

  /** Reactive effective-permissions snapshot — re-evaluated on every permissions load. */
  private readonly permissions = toSignal(
    this.permissionState.permissions$,
    { initialValue: [] as EffectivePermissionDto[] },
  );

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
  readonly navOpen  = signal(false);

  /** Nav links visible to the current user based on their effective permissions. */
  readonly visibleLinks = computed(() => {
    const user = this.currentUser();
    if (!user) return [];

    const perms = this.permissions();
    const hasPerm = (area: string, action: string): boolean =>
      perms.some(
        p =>
          p.area.toLowerCase()   === area.toLowerCase()   &&
          p.action.toLowerCase() === action.toLowerCase() &&
          p.isAllowed,
      );

    return NAV_LINKS.filter(link => {
      if (link.visibleWhen === null) return true;
      if ('role' in link.visibleWhen) return user.roles.includes(link.visibleWhen.role);
      return hasPerm(link.visibleWhen.area, link.visibleWhen.action);
    });
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
    this.menuOpen.set(false);
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
