import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnDestroy,
  ViewChild,
  inject,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { EmailTemplateService } from '../../../services/email-template.service';
import { EmailTemplate } from '../../../models/email-template.model';
import { ToastService } from '../../../core/services/toast.service';

declare const bootstrap: {
  Modal: new (el: HTMLElement, opts?: object) => { show(): void; hide(): void; dispose(): void };
};

interface TemplateForm {
  subject:  FormControl<string>;
  htmlBody: FormControl<string>;
  isActive: FormControl<boolean>;
}

const PLACEHOLDER_MAP: Record<string, string[]> = {
  'ActionItem':  ['ActionId', 'Title', 'Status', 'Priority', 'DueDate', 'AssigneeName', 'ProjectName', 'MilestoneName', 'ItemUrl'],
  'Milestone':   ['MilestoneCode', 'MilestoneName', 'ProjectName', 'ProjectCode', 'Status', 'PlannedDueDate', 'CompletionPercentage', 'ItemUrl'],
  'User':        ['UserName', 'Email', 'Role', 'LoginUrl'],
  'Project':     ['ProjectCode', 'ProjectName', 'Status', 'ProjectManagerName', 'ItemUrl'],
};

@Component({
  selector: 'app-email-template-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './email-template-edit.component.html',
  styleUrl: './email-template-edit.component.scss',
})
export class EmailTemplateEditComponent implements AfterViewInit, OnDestroy {
  @Input({ required: true }) templateId!: string;
  @ViewChild('modalEl') modalEl!: ElementRef<HTMLElement>;

  saved = output<void>();
  closed = output<void>();

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(EmailTemplateService);
  private readonly toast = inject(ToastService);
  private readonly sanitizer = inject(DomSanitizer);

  private bsModal!: { show(): void; hide(): void; dispose(): void };

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  template = signal<EmailTemplate | null>(null);
  showPlaceholders = signal(false);
  showPreview = signal(false);

  form: FormGroup<TemplateForm> = this.fb.group({
    subject:  this.fb.nonNullable.control('', Validators.required),
    htmlBody: this.fb.nonNullable.control('', Validators.required),
    isActive: this.fb.nonNullable.control(true),
  });

  get previewHtml(): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(this.form.controls.htmlBody.value);
  }

  get placeholders(): string[] {
    const tpl = this.template();
    if (!tpl) return [];
    const key = tpl.templateKey.split('.')[0];
    return PLACEHOLDER_MAP[key] ?? [];
  }

  ngAfterViewInit(): void {
    this.bsModal = new bootstrap.Modal(this.modalEl.nativeElement, { backdrop: 'static' });

    this.modalEl.nativeElement.addEventListener('hidden.bs.modal', () => {
      this.closed.emit();
    });

    this.loadTemplate();
  }

  ngOnDestroy(): void {
    this.bsModal?.dispose();
  }

  private loadTemplate(): void {
    this.loading.set(true);
    this.service.getById(this.templateId).subscribe({
      next: res => {
        if (res.success) {
          this.template.set(res.data);
          this.form.patchValue({
            subject: res.data.subject,
            htmlBody: res.data.htmlBody,
            isActive: res.data.isActive,
          });
        }
        this.loading.set(false);
        this.bsModal.show();
      },
      error: () => {
        this.error.set('Failed to load template.');
        this.loading.set(false);
        this.bsModal.show();
      },
    });
  }

  submit(): void {
    if (this.form.invalid) return;

    this.saving.set(true);
    this.error.set('');

    const val = this.form.getRawValue();
    this.service.update(this.templateId, {
      subject: val.subject,
      htmlBody: val.htmlBody,
      isActive: val.isActive,
    }).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.success('Template updated successfully.');
          this.bsModal.hide();
          this.saved.emit();
        } else {
          this.error.set(res.message || 'Update failed.');
        }
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Failed to update template.');
      },
    });
  }

  close(): void {
    this.bsModal.hide();
  }
}
