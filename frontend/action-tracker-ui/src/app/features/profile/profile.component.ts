import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProfileService } from '../../core/services/profile.service';
import { AuthService } from '../../core/services/auth.service';
import { EmployeeProfile } from '../../core/models/employee-profile.model';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, BreadcrumbComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent implements OnInit {
  private readonly profileSvc = inject(ProfileService);
  private readonly authSvc = inject(AuthService);

  readonly profile = signal<EmployeeProfile | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  displayName = '';
  userEmail = '';
  loginProvider = '';
  roles: string[] = [];

  ngOnInit(): void {
    this.authSvc.currentUser$.subscribe(user => {
      if (user) {
        this.displayName = user.displayName;
        this.userEmail = user.email;
        this.loginProvider = user.loginProvider;
        this.roles = user.roles;
      }
    });

    this.profileSvc.getMyProfile().subscribe({
      next: res => {
        this.profile.set(res.data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load employee profile.');
        this.loading.set(false);
      },
    });
  }

  getInitial(): string {
    const name = this.profile()?.employeeName || this.displayName;
    return (name?.charAt(0) || '?').toUpperCase();
  }
}
