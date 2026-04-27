import { Component, OnInit, AfterViewInit, ElementRef, ViewChild, Renderer2, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MsalService } from '@azure/msal-angular';

import { AuthService } from '../../../core/services/auth.service';
import { LoginRequest } from '../../../core/models/auth.models';
import { environment } from '../../../../environments/environment';

const MSAL_CONFIGURED =
  !!environment.msalConfig.auth.clientId &&
  !environment.msalConfig.auth.authority.includes('<TENANT_ID>');

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit, AfterViewInit {
  @ViewChild('particleHost', { static: false }) particleHost!: ElementRef<HTMLDivElement>;

  private readonly fb          = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly msalService = inject(MsalService);
  private readonly router      = inject(Router);
  private readonly renderer    = inject(Renderer2);

  readonly form: FormGroup = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
  });

  readonly loading     = signal(false);
  readonly msalLoading = signal(false);
  readonly errorMsg    = signal<string | null>(null);
  readonly showPwd     = signal(false);

  readonly currentYear = new Date().getFullYear();

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

  ngAfterViewInit(): void {
    this.spawnParticles();
  }

  private spawnParticles(): void {
    if (!this.particleHost) return;
    const host = this.particleHost.nativeElement;
    const colors = [
      { c: '#00E5FF', w: 40 },
      { c: '#7C5CFC', w: 20 },
      { c: '#FF6B9D', w: 12 },
      { c: '#22c55e', w: 10 },
      { c: '#f59e0b', w: 10 },
      { c: '#3b82f6', w: 8 },
    ];
    const total = 60;

    const pickColor = (): string => {
      const r = Math.random() * 100;
      let acc = 0;
      for (const { c, w } of colors) {
        acc += w;
        if (r < acc) return c;
      }
      return colors[0].c;
    };

    const pickSize = (): number => {
      const r = Math.random() * 100;
      if (r < 40) return 4 + Math.random() * 4;        // small:  4–8px   (40%)
      if (r < 75) return 8 + Math.random() * 6;         // medium: 8–14px  (35%)
      return 14 + Math.random() * 10;                   // large: 14–24px  (25%)
    };

    for (let i = 0; i < total; i++) {
      const el = this.renderer.createElement('span');
      const size = pickSize();
      const color = pickColor();
      const duration = 14 + Math.random() * 22;
      const dx = (Math.random() - 0.5) * 150;
      const dy = (Math.random() - 0.5) * 150;

      this.renderer.addClass(el, 'dot');
      this.renderer.setStyle(el, 'width', `${size}px`);
      this.renderer.setStyle(el, 'height', `${size}px`);
      this.renderer.setStyle(el, 'background', color);
      this.renderer.setStyle(el, 'boxShadow', `0 0 ${size * 2.5}px ${color}`);
      this.renderer.setStyle(el, 'left', `${Math.random() * 100}%`);
      this.renderer.setStyle(el, 'top', `${Math.random() * 100}%`);
      this.renderer.setStyle(el, 'animationDuration', `${duration}s`);
      this.renderer.setStyle(el, 'animationDelay', `${-Math.random() * duration}s`);
      this.renderer.setStyle(el, '--dx', `${dx}px`);
      this.renderer.setStyle(el, '--dy', `${dy}px`);
      this.renderer.appendChild(host, el);
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
    if (!MSAL_CONFIGURED) {
      this.errorMsg.set(
        'Microsoft login is not configured. Set clientId and authority in environment.ts.',
      );
      return;
    }

    this.msalLoading.set(true);
    this.errorMsg.set(null);

    // Redirect navigates the entire page to Microsoft and never returns here.
    // The response is processed by handleRedirectPromise() in app.config.ts
    // when the browser lands back on /auth_fallback.
    this.msalService
      .loginRedirect({ scopes: environment.msalScopes })
      .subscribe({
        error: (err) => {
          this.errorMsg.set(
            err?.error?.message ?? 'Microsoft sign-in failed. Please try again.',
          );
          this.msalLoading.set(false);
        },
      });
  }
}
