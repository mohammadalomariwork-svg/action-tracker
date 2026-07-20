import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MailServerConfigService } from '../../../services/mail-server-config.service';
import { MailServerConfig, UpdateMailServerConfig } from '../../../models/mail-server-config.model';
import { ToastService } from '../../../core/services/toast.service';
import { AdminBreadcrumbComponent } from '../components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

interface MailServerForm {
  host:      FormControl<string>;
  port:      FormControl<number>;
  useSsl:    FormControl<boolean>;
  fromEmail: FormControl<string>;
  fromName:  FormControl<string>;
  username:  FormControl<string>;
  password:  FormControl<string>;
}

@Component({
  selector: 'app-mail-server-config',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    ReactiveFormsModule,
    AdminBreadcrumbComponent,
    PageHeaderComponent,
  ],
  templateUrl: './mail-server-config.component.html',
  styleUrl: './mail-server-config.component.scss',
})
export class MailServerConfigComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(MailServerConfigService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly saving  = signal(false);
  readonly config  = signal<MailServerConfig | null>(null);
  readonly showPassword = signal(false);

  readonly form: FormGroup<MailServerForm> = this.fb.group({
    host:      this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(256)]),
    port:      this.fb.nonNullable.control(587, [Validators.required, Validators.min(1), Validators.max(65535)]),
    useSsl:    this.fb.nonNullable.control(true),
    fromEmail: this.fb.nonNullable.control('', [Validators.required, Validators.email, Validators.maxLength(256)]),
    fromName:  this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(256)]),
    username:  this.fb.nonNullable.control('', Validators.maxLength(256)),
    password:  this.fb.nonNullable.control(''),
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service.get().subscribe({
      next: res => {
        if (res.success && res.data) {
          this.config.set(res.data);
          this.form.patchValue({
            host:      res.data.host,
            port:      res.data.port,
            useSsl:    res.data.useSsl,
            fromEmail: res.data.fromEmail,
            fromName:  res.data.fromName,
            username:  res.data.username,
            password:  '',
          });
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.error('Failed to load mail server configuration.');
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.error('Please correct the highlighted fields.');
      return;
    }

    const v = this.form.getRawValue();
    const dto: UpdateMailServerConfig = {
      host:      v.host.trim(),
      port:      v.port,
      useSsl:    v.useSsl,
      fromEmail: v.fromEmail.trim(),
      fromName:  v.fromName.trim(),
      username:  v.username.trim(),
      password:  v.password ? v.password : null,
    };

    this.saving.set(true);
    this.service.save(dto).subscribe({
      next: res => {
        if (res.success) {
          this.config.set(res.data);
          this.form.controls.password.reset('');
          this.toast.success('Mail server configuration saved.');
        } else {
          this.toast.error(res.message || 'Failed to save configuration.');
        }
        this.saving.set(false);
      },
      error: err => {
        const msg = err?.error?.message || 'Failed to save configuration.';
        this.toast.error(msg);
        this.saving.set(false);
      },
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }
}
