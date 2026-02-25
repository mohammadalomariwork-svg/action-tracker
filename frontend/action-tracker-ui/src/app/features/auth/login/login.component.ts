import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly form: FormGroup = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
  });

  readonly loading  = signal(false);
  readonly errorMsg = signal<string | null>(null);
  readonly showPwd  = signal(false);

  readonly features = [
    { icon: '📋', text: 'Track and manage action items across departments' },
    { icon: '📊', text: 'Real-time KPI dashboards for leadership' },
    { icon: '🔔', text: 'Automatic escalation for overdue actions' },
    { icon: '📁', text: 'Export reports for meetings and reviews' },
  ];

  togglePwd(): void {
    this.showPwd.update(v => !v);
  }

  get email()    { return this.form.get('email')!; }
  get password() { return this.form.get('password')!; }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMsg.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        const msg =
          err?.error?.message ||
          err?.error?.errors?.[0] ||
          'Invalid email or password. Please try again.';
        this.errorMsg.set(msg);
      },
    });
  }
}
