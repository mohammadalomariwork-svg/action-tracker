import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EmailTemplateService } from '../../../services/email-template.service';
import { EmailTemplateListItem, EmailLog } from '../../../models/email-template.model';
import { ToastService } from '../../../core/services/toast.service';
import { AdminBreadcrumbComponent } from '../components/shared/admin-breadcrumb/admin-breadcrumb.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { EmailTemplateEditComponent } from '../email-template-edit/email-template-edit.component';

@Component({
  selector: 'app-email-templates-page',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule,
    AdminBreadcrumbComponent, PageHeaderComponent,
    EmailTemplateEditComponent,
  ],
  templateUrl: './email-templates-page.component.html',
  styleUrl: './email-templates-page.component.scss',
})
export class EmailTemplatesPageComponent implements OnInit {
  private readonly service = inject(EmailTemplateService);
  private readonly toast = inject(ToastService);

  activeTab = signal<'templates' | 'logs'>('templates');

  // Templates tab
  templates = signal<EmailTemplateListItem[]>([]);
  loadingTemplates = signal(false);

  // Logs tab
  logs = signal<EmailLog[]>([]);
  loadingLogs = signal(false);
  logsPage = signal(1);
  logsPageSize = signal(20);
  logsTotalPages = signal(0);
  logsTotalCount = signal(0);
  logsFilterKey = signal('');
  logsFilterStatus = signal('');

  // Edit modal
  editTemplateId = signal<string | null>(null);

  ngOnInit(): void {
    this.loadTemplates();
  }

  selectTab(tab: 'templates' | 'logs'): void {
    this.activeTab.set(tab);
    if (tab === 'logs' && this.logs().length === 0) {
      this.loadLogs();
    }
  }

  loadTemplates(): void {
    this.loadingTemplates.set(true);
    this.service.getAll().subscribe({
      next: res => {
        if (res.success) this.templates.set(res.data);
        this.loadingTemplates.set(false);
      },
      error: () => {
        this.toast.error('Failed to load templates.');
        this.loadingTemplates.set(false);
      },
    });
  }

  toggleActive(tpl: EmailTemplateListItem): void {
    this.service.getById(tpl.id).subscribe({
      next: res => {
        if (!res.success) return;
        const full = res.data;
        this.service.update(tpl.id, {
          subject: full.subject,
          htmlBody: full.htmlBody,
          isActive: !tpl.isActive,
        }).subscribe({
          next: updated => {
            if (updated.success) {
              tpl.isActive = !tpl.isActive;
              this.toast.success(`Template ${tpl.isActive ? 'activated' : 'deactivated'}.`);
            }
          },
          error: () => this.toast.error('Failed to toggle template.'),
        });
      },
      error: () => this.toast.error('Failed to load template for toggle.'),
    });
  }

  openEdit(id: string): void {
    this.editTemplateId.set(id);
  }

  onEditSaved(): void {
    this.editTemplateId.set(null);
    this.loadTemplates();
  }

  onEditClosed(): void {
    this.editTemplateId.set(null);
  }

  // ── Logs ──────────────────────────────────────────────────────

  loadLogs(): void {
    this.loadingLogs.set(true);
    const key = this.logsFilterKey() || undefined;
    const status = this.logsFilterStatus() || undefined;
    this.service.getLogs(this.logsPage(), this.logsPageSize(), key, status).subscribe({
      next: res => {
        if (res.success) {
          this.logs.set(res.data.items);
          this.logsTotalPages.set(res.data.totalPages);
          this.logsTotalCount.set(res.data.totalCount);
        }
        this.loadingLogs.set(false);
      },
      error: () => {
        this.toast.error('Failed to load email logs.');
        this.loadingLogs.set(false);
      },
    });
  }

  applyLogsFilter(): void {
    this.logsPage.set(1);
    this.loadLogs();
  }

  goToLogsPage(page: number): void {
    if (page < 1 || page > this.logsTotalPages()) return;
    this.logsPage.set(page);
    this.loadLogs();
  }

  getStatusBadgeClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'sent':   return 'bg-success';
      case 'failed': return 'bg-danger';
      case 'queued': return 'bg-primary';
      default:       return 'bg-secondary';
    }
  }

  truncate(value: string, max: number): string {
    return value.length > max ? value.substring(0, max) + '...' : value;
  }
}
