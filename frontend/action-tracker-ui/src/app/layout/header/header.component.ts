import { Component, inject, signal, computed, HostListener } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AsyncPipe } from '@angular/common';
import { AuthService } from '../../core/services/auth.service';

interface NavLink {
  label: string;
  path: string;
  roles: string[] | null; // null = all authenticated users
}

const NAV_LINKS: NavLink[] = [
  { label: 'Dashboard', path: '/dashboard', roles: null },
  { label: 'Management View', path: '/management', roles: ['Admin', 'Manager'] },
  { label: 'Actions', path: '/actions', roles: null },
  { label: 'Reports', path: '/reports', roles: ['Admin', 'Manager'] },
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

  readonly currentUser$ = this.authService.currentUser$;
  readonly menuOpen = signal(false);

  readonly visibleLinks = computed(() => {
    const user = this.authService.getCurrentUser();
    return NAV_LINKS.filter(link =>
      link.roles === null || (user && link.roles.includes(user.role))
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
  onDocumentClick(target: HTMLElement): void {
    if (!target.closest('.nav-user') && !target.closest('.hamburger')) {
      this.menuOpen.set(false);
    }
  }
}
