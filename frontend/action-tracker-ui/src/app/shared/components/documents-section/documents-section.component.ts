import {
  Component, OnInit, ChangeDetectionStrategy,
  inject, signal, input,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';

import { DocumentService } from '../../../core/services/document.service';
import { ToastService } from '../../../core/services/toast.service';
import { DocumentInfo } from '../../../core/models/document.model';

@Component({
  selector: 'app-documents-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DatePipe],
  templateUrl: './documents-section.component.html',
  styleUrl: './documents-section.component.scss',
})
export class DocumentsSectionComponent implements OnInit {
  readonly entityType = input.required<string>();
  readonly entityId = input.required<string>();

  private readonly docSvc = inject(DocumentService);
  private readonly toastSvc = inject(ToastService);

  readonly documents = signal<DocumentInfo[]>([]);
  readonly loading = signal(false);

  // Upload form
  readonly docName = signal('');
  readonly docFile = signal<File | null>(null);
  readonly uploading = signal(false);

  ngOnInit(): void {
    this.loadDocuments();
  }

  private loadDocuments(): void {
    this.loading.set(true);
    this.docSvc.getByEntity(this.entityType(), this.entityId()).subscribe({
      next: r => {
        this.documents.set(r.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
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
    if (!file || !name) return;

    this.uploading.set(true);
    this.docSvc.upload(this.entityType(), this.entityId(), name, file).subscribe({
      next: r => {
        this.documents.update(list => [r.data, ...list]);
        this.docName.set('');
        this.docFile.set(null);
        this.uploading.set(false);
        this.toastSvc.success('Document uploaded.');
      },
      error: (err) => {
        this.uploading.set(false);
        const msg = err?.error?.message || 'Failed to upload document.';
        this.toastSvc.error(msg);
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

  deleteDocument(docId: string): void {
    this.docSvc.delete(docId).subscribe({
      next: () => {
        this.documents.update(list => list.filter(d => d.id !== docId));
        this.toastSvc.success('Document deleted.');
      },
      error: () => this.toastSvc.error('Failed to delete document.'),
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }
}
