import { Component, inject, signal, computed, HostListener, OnInit, OnDestroy } from '@angular/core';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { AsyncPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith, Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
import { PermissionStateService } from '../../features/permissions/services/permission-state.service';
import { EffectivePermissionDto } from '../../features/permissions/models/user-permission.model';
import { ThemeService } from '../../services/theme.service';
import { WorkflowStateService } from '../../services/workflow-state.service';
import { NotificationService } from '../../core/services/notification.service';

interface NavLink {
  label: string;
  icon: string;
  path: string;
  visibleWhen: null | { area: string; action: string } | { role: string };
  alsoActivePrefixes?: string[];
}

const NAV_LINKS: NavLink[] = [
  {
    label: 'Dashboards',
    icon: 'bi-speedometer2',
    path: '/dashboard',
    visibleWhen: { area: 'Dashboard', action: 'View' },
  },
  {
    label: 'My Actions',
    icon: 'bi-check2-square',
    path: '/actions',
    visibleWhen: null,
  },
  {
    label: 'My Projects',
    icon: 'bi-folder2-open',
    path: '/projects/my',
    visibleWhen: null,
  },
  {
    label: 'My Approvals',
    icon: 'bi-clipboard-check',
    path: '/approvals',
    visibleWhen: null,
  },
  {
    label: 'Reports',
    icon: 'bi-bar-chart-line',
    path: '/reports',
    visibleWhen: { area: 'Reports', action: 'View' },
  },
  {
    label: 'Team Actions',
    icon: 'bi-people',
    path: '/management',
    visibleWhen: { area: 'Action Items', action: 'View' },
    alsoActivePrefixes: ['/projects/new', '/projects/edit', '/action-items'],
  },
  {
    label: 'Workspaces',
    icon: 'bi-collection',
    path: '/workspaces',
    visibleWhen: { area: 'Workspaces', action: 'View' },
  },
  {
    label: 'Notifications',
    icon: 'bi-bell',
    path: '/notifications',
    visibleWhen: null,
  },
  {
    label: 'Admin Panel',
    icon: 'bi-shield-lock',
    path: '/admin',
    visibleWhen: { role: 'Admin' },
  },
];

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, AsyncPipe, NotificationBellComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent implements OnInit, OnDestroy {
  private readonly authService       = inject(AuthService);
  private readonly permissionState   = inject(PermissionStateService);
  private readonly router            = inject(Router);
  private readonly themeService      = inject(ThemeService);
  private readonly workflowState     = inject(WorkflowStateService);
  private readonly notificationSvc  = inject(NotificationService);

  readonly currentUser$ = this.authService.currentUser$;
  readonly pendingApprovalCount = toSignal(this.workflowState.pendingCount$, { initialValue: 0 });

  private refreshInterval: ReturnType<typeof setInterval> | null = null;
  private userSub: Subscription | null = null;

  private readonly currentUser = toSignal(this.authService.currentUser$, { initialValue: null });

  private readonly permissions = toSignal(
    this.permissionState.permissions$,
    { initialValue: [] as EffectivePermissionDto[] },
  );

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly menuOpen  = signal(false);
  readonly isNavOpen = signal(false);
  readonly isDark    = toSignal(this.themeService.isDark$, { initialValue: true });

  ngOnInit(): void {
    // Register callback for real-time workflow notification refresh
    this.notificationSvc.registerWorkflowRefresh(() => {
      this.workflowState.refreshPendingCount();
    });

    this.userSub = this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.workflowState.refreshPendingCount();
        this.refreshInterval = setInterval(() => {
          this.workflowState.refreshPendingCount();
        }, 60_000);
      } else {
        if (this.refreshInterval) {
          clearInterval(this.refreshInterval);
          this.refreshInterval = null;
        }
      }
    });
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
    this.userSub?.unsubscribe();
  }

  /** Nav links visible to the current user based on their effective permissions. */
  readonly visibleLinks = computed(() => {
    const user = this.currentUser();
    if (!user) return [];

    const perms = this.permissions();
    const hasPerm = (area: string, action: string): boolean =>
      perms.some(
        p =>
          p.areaName.toLowerCase()   === area.toLowerCase()   &&
          p.actionName.toLowerCase() === action.toLowerCase() &&
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

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  toggleMenu(): void {
    this.menuOpen.update(v => !v);
  }

  openNav(): void {
    this.isNavOpen.set(true);
    this.menuOpen.set(false);
  }

  closeNav(): void {
    this.isNavOpen.set(false);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
    this.isNavOpen.set(false);
  }

  logout(): void {
    this.permissionState.clearPermissions();
    this.workflowState.clearPendingCount();
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
