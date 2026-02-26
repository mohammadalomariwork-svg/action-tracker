import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MsalService } from '@azure/msal-angular';

import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../core/models/auth.models';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  private readonly fb          = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly msalService = inject(MsalService);
  private readonly router      = inject(Router);

  readonly form: FormGroup = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
  });

  readonly loading     = signal(false);
  readonly msalLoading = signal(false);
  readonly errorMsg    = signal<string | null>(null);
  readonly showPwd     = signal(false);

  readonly features = [
    { icon: '📋', text: 'Track and manage action items across departments' },
    { icon: '📊', text: 'Real-time KPI dashboards for leadership' },
    { icon: '🔔', text: 'Automatic escalation for overdue actions' },
    { icon: '📁', text: 'Export reports for meetings and reviews' },
  ];

  // ── Lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/dashboard']);
    }
  }

  // ── Form accessors ─────────────────────────────────────────────────────────

  get email()    { return this.form.get('email')!;    }
  get password() { return this.form.get('password')!; }

  // ── Actions ────────────────────────────────────────────────────────────────

  togglePwd(): void {
    this.showPwd.update(v => !v);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMsg.set(null);

    const request = this.form.getRawValue() as LoginRequest;

    this.authService.login(request).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMsg.set(
          err?.error?.message
            || err?.error?.errors?.[0]
            || 'Invalid email or password. Please try again.',
        );
      },
    });
  }

  onMicrosoftLogin(): void {
    this.msalLoading.set(true);
    this.errorMsg.set(null);

    this.msalService.loginPopup({ scopes: environment.msalScopes }).subscribe({
      next: (result) => {
        this.authService.loginWithAzureAd(result.accessToken).subscribe({
          next: () => {
            this.msalLoading.set(false);
            this.router.navigate(['/dashboard']);
          },
          error: (err) => {
            this.msalLoading.set(false);
            this.errorMsg.set(
              err?.error?.message ?? 'Microsoft sign-in failed. Please try again.',
            );
          },
        });
      },
      error: (err) => {
        const cancelled =
          err?.errorCode === 'user_cancelled' ||
          err?.errorCode === 'popup_window_error';
        if (!cancelled) {
          this.errorMsg.set('Microsoft sign-in failed. Please try again.');
        }
        this.msalLoading.set(false);
      },
    });
  }
}
