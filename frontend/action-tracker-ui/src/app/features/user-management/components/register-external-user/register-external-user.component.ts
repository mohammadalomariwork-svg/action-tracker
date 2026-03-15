import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';

import { UserManagementService } from '../../services/user-management.service';
import { RegisterUserResponse } from '../../models/user-management.models';
import { AdminBreadcrumbComponent } from '../../../admin/components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

interface ExternalUserForm {
  fullName:        FormControl<string>;
  email:           FormControl<string>;
  phoneNumber:     FormControl<string>;
  password:        FormControl<string>;
  confirmPassword: FormControl<string>;
  roleName:        FormControl<string>;
}

@Component({
  selector: 'app-register-external-user',
  standalone: true,
  imports: [ReactiveFormsModule, AdminBreadcrumbComponent, PageHeaderComponent],
  templateUrl: './register-external-user.component.html',
  styleUrl: './register-external-user.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterExternalUserComponent implements OnInit {
  private readonly userMgmtService = inject(UserManagementService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  // ── State ────────────────────────────────────────────────────────────────────
  readonly loading = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal(false);
  readonly availableRoles = signal<string[]>([]);

  ngOnInit(): void {
    this.userMgmtService
      .getRoles()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (roles) => this.availableRoles.set(roles) });
  }

  // ── Form ─────────────────────────────────────────────────────────────────────
  readonly form: FormGroup<ExternalUserForm> = this.fb.group(
    {
      fullName: this.fb.nonNullable.control('', [
        Validators.required,
        Validators.minLength(2),
      ]),
      email: this.fb.nonNullable.control('', [
        Validators.required,
        Validators.email,
      ]),
      phoneNumber: this.fb.nonNullable.control(''),
      password: this.fb.nonNullable.control('', [
        Validators.required,
        Validators.minLength(8),
      ]),
      confirmPassword: this.fb.nonNullable.control('', [Validators.required]),
      roleName: this.fb.nonNullable.control('', [Validators.required]),
    } satisfies ExternalUserForm,
    { validators: RegisterExternalUserComponent.passwordMatch }
  ) as FormGroup<ExternalUserForm>;

  // ── Static validator ─────────────────────────────────────────────────────────
  static passwordMatch(group: AbstractControl): ValidationErrors | null {
    const pw  = group.get('password')?.value;
    const cpw = group.get('confirmPassword')?.value;
    return pw && cpw && pw !== cpw ? { passwordMismatch: true } : null;
  }

  // ── Helpers ───────────────────────────────────────────────────────────────────
  ctrl(name: keyof ExternalUserForm) {
    return this.form.controls[name];
  }

  isInvalid(name: keyof ExternalUserForm): boolean {
    const c = this.ctrl(name);
    return c.invalid && c.touched;
  }

  get confirmPasswordMismatch(): boolean {
    return (
      this.form.hasError('passwordMismatch') &&
      this.ctrl('confirmPassword').touched
    );
  }

  toggleShowPassword(): void {
    this.showPassword.update(v => !v);
  }

  // ── Submit ────────────────────────────────────────────────────────────────────
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { fullName, email, phoneNumber, password, roleName } =
      this.form.getRawValue();

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.userMgmtService
      .registerExternalUser({
        fullName,
        email,
        password,
        roleName,
        ...(phoneNumber ? { phoneNumber } : {}),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: RegisterUserResponse) => {
          this.loading.set(false);
          this.successMessage.set(
            `User "${res.fullName}" registered successfully.`
          );
          setTimeout(() => this.router.navigate(['/admin/users']), 2000);
        },
        error: (err: HttpErrorResponse) => {
          this.loading.set(false);
          if (err.status === 409) {
            this.errorMessage.set('Email or username already exists.');
          } else {
            this.errorMessage.set(
              err.error?.message ?? 'An unexpected error occurred. Please try again.'
            );
          }
        },
      });
  }

  cancel(): void {
    this.router.navigate(['/admin/users']);
  }
}
