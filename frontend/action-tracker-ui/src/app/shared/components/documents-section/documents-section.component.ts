import {
  Component, ChangeDetectionStrategy, Injector,
  afterNextRender, computed, effect, inject, input, output, signal, untracked, viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';

import { DocumentService } from '../../../core/services/document.service';
import { ToastService } from '../../../core/services/toast.service';
import { DocumentInfo } from '../../../core/models/document.model';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';

/**
 * Files of one record (`/api/documents`). Used for action items, sub-items and
 * action updates (WO-2026-11 A-4), projects, milestones, risks and KPI evidence.
 *
 * WO-2026-11 A-4 inputs, all optional so the other pages behave as before:
 * `canUpload` (show the upload form), `lockedNote` (shown instead of the form,
 * e.g. on a Done action item), `maxFiles` ("n of max", upload disabled at the
 * cap), `compact` (inside a sub-item row or an update). The delete button
 * shows only when the server says the caller may delete (`canDelete`) and asks
 * for confirmation. `countChange` reports the number of files after every change.
 */
@Component({
  selector: 'app-documents-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe, ConfirmDialogComponent],
  templateUrl: './documents-section.component.html',
  styleUrl: './documents-section.component.scss',
})
export class DocumentsSectionComponent {
  readonly entityType = input.required<string>();
  readonly entityId = input.required<string>();
  readonly canUpload = input(true);
  readonly lockedNote = input<string | null>(null);
  readonly maxFiles = input<number | null>(null);
  readonly compact = input(false);

  readonly countChange = output<number>();

  private readonly docSvc = inject(DocumentService);
  private readonly toastSvc = inject(ToastService);
  private readonly injector = inject(Injector);

  private readonly deleteDialog = viewChild<ConfirmDialogComponent>('deleteDialog');

  readonly documents = signal<DocumentInfo[]>([]);
  readonly loading = signal(false);

  // Upload form
  readonly docName = signal('');
  readonly docFile = signal<File | null>(null);
  readonly uploading = signal(false);

  // Delete
  readonly pendingDelete = signal<DocumentInfo | null>(null);
  private returnFocusTo: HTMLElement | null = null;

  /** Unique element-id prefix (a page can hold several sections). */
  readonly idPrefix = computed(() => `docs-${this.entityType()}-${this.entityId()}`);
  readonly atCap = computed(() => {
    const max = this.maxFiles();
    return max != null && this.documents().length >= max;
  });
  readonly showForm = computed(() => this.canUpload() && !this.lockedNote());

  constructor() {
    effect(() => {
      this.entityType();
      this.entityId();
      untracked(() => this.loadDocuments());
    });
  }

  private loadDocuments(): void {
    this.loading.set(true);
    this.docSvc.getByEntity(this.entityType(), this.entityId()).subscribe({
      next: r => {
        this.setDocuments(r.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private setDocuments(list: DocumentInfo[]): void {
    this.documents.set(list);
    this.countChange.emit(list.length);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.docFile.set(input.files[0]);
    }
  }

  uploadDocument(): void {
    const file = this.docFile();
    const name = this.docName().trim();
    if (!file || !name || this.atCap()) return;

    this.uploading.set(true);
    this.docSvc.upload(this.entityType(), this.entityId(), name, file).subscribe({
      next: r => {
        this.setDocuments([r.data, ...this.documents()]);
        this.docName.set('');
        this.docFile.set(null);
        this.uploading.set(false);
        this.toastSvc.success('Document uploaded.');
      },
      error: (err) => {
        this.uploading.set(false);
        const msg = err?.error?.message || 'Failed to upload document.';
        this.toastSvc.error(msg);
        if (err?.status === 409 || err?.status === 404) this.loadDocuments();
      },
    });
  }

  downloadDocument(doc: DocumentInfo): void {
    this.docSvc.download(doc.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = doc.fileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastSvc.error('Failed to download document.'),
    });
  }

  /** Delete button → confirm dialog (shown only when the server allows the delete). */
  askDelete(doc: DocumentInfo, trigger: HTMLElement): void {
    this.pendingDelete.set(doc);
    this.returnFocusTo = trigger;
    this.deleteDialog()?.open();
  }

  onDeleteConfirmed(confirmed: boolean): void {
    const doc = this.pendingDelete();
    this.pendingDelete.set(null);
    if (!confirmed || !doc) { this.restoreFocus(); return; }
    this.deleteDocument(doc.id);
  }

  deleteDocument(docId: string): void {
    this.docSvc.delete(docId).subscribe({
      next: () => {
        this.setDocuments(this.documents().filter(d => d.id !== docId));
        this.toastSvc.success('Document deleted.');
        this.returnFocusTo = null;
        afterNextRender(() => document.getElementById(`${this.idPrefix()}-heading`)?.focus(), { injector: this.injector });
      },
      error: (err) => {
        this.toastSvc.error(err?.error?.message || 'Failed to delete document.');
        if (err?.status === 409 || err?.status === 403 || err?.status === 404) this.loadDocuments();
        this.restoreFocus();
      },
    });
  }

  private restoreFocus(): void {
    const target = this.returnFocusTo;
    this.returnFocusTo = null;
    if (target) afterNextRender(() => { if (document.contains(target)) target.focus(); }, { injector: this.injector });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }
}
