import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DocumentService } from '../../../services/document.service';
import { DocumentInfo } from '../../../models/project.models';

const ALLOWED_TYPES = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'image/png',
  'image/jpeg',
  'image/gif',
  'text/plain',
  'text/csv',
  'application/zip',
  'application/x-rar-compressed',
];

const ALLOWED_EXTENSIONS = '.pdf, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .png, .jpg, .jpeg, .gif, .txt, .csv, .zip, .rar';
const MAX_FILE_SIZE = 20 * 1024 * 1024; // 20 MB

@Component({
  selector: 'app-document-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './document-panel.component.html',
  styleUrl: './document-panel.component.scss',
})
export class DocumentPanelComponent implements OnInit {
  @Input() projectId?: string;
  @Input() actionItemId?: string;

  private readonly documentService = inject(DocumentService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  documents: DocumentInfo[] = [];
  isLoading = false;
  isUploading = false;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  showUploadForm = false;
  uploadForm!: FormGroup;
  selectedFile: File | null = null;
  isDragOver = false;

  readonly allowedExtensions = ALLOWED_EXTENSIONS;

  ngOnInit(): void {
    this.buildForm();
    this.loadDocuments();
  }

  // ── File handling ──────────────────────────────────────────────────────────

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.validateAndSetFile(input.files[0]);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
    if (event.dataTransfer?.files?.length) {
      this.validateAndSetFile(event.dataTransfer.files[0]);
    }
  }

  // ── Actions ─────────────────────────────────────────────────────────────────

  toggleUploadForm(): void {
    this.showUploadForm = !this.showUploadForm;
    if (this.showUploadForm) {
      this.uploadForm.reset({ title: '' });
      this.selectedFile = null;
      this.errorMessage = null;
    }
  }

  onUpload(): void {
    if (this.uploadForm.invalid || !this.selectedFile) {
      this.uploadForm.markAllAsTouched();
      if (!this.selectedFile) {
        this.errorMessage = 'Please select a file to upload.';
      }
      return;
    }

    this.isUploading = true;
    this.errorMessage = null;

    const title = this.uploadForm.get('title')!.value;
    const file = this.selectedFile;

    const upload$ = this.projectId
      ? this.documentService.uploadProjectDocument(this.projectId, title, file)
      : this.actionItemId
        ? this.documentService.uploadActionDocument(this.actionItemId, title, file)
        : null;

    if (!upload$) {
      this.errorMessage = 'No project or action item context for upload.';
      this.isUploading = false;
      return;
    }

    upload$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isUploading = false;
        this.showUploadForm = false;
        this.selectedFile = null;
        this.successMessage = 'Document uploaded successfully.';
        this.loadDocuments();
        setTimeout(() => (this.successMessage = null), 3000);
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to upload document.';
        this.isUploading = false;
      },
    });
  }

  onDownload(doc: DocumentInfo): void {
    const type = this.projectId ? 'project' : 'action';

    this.documentService
      .downloadDocument(doc.id, type)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = doc.fileName;
          anchor.click();
          URL.revokeObjectURL(url);
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to download document.';
        },
      });
  }

  onDelete(id: string): void {
    if (!confirm('Are you sure you want to delete this document?')) return;

    const delete$ = this.projectId
      ? this.documentService.deleteProjectDocument(id)
      : this.documentService.deleteActionDocument(id);

    delete$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.loadDocuments(),
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to delete document.';
      },
    });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  getFileIcon(contentType: string): string {
    if (contentType.includes('pdf')) return 'bi-file-earmark-pdf-fill';
    if (contentType.includes('word') || contentType.includes('document')) return 'bi-file-earmark-word-fill';
    if (contentType.includes('excel') || contentType.includes('spreadsheet')) return 'bi-file-earmark-excel-fill';
    if (contentType.includes('powerpoint') || contentType.includes('presentation')) return 'bi-file-earmark-ppt-fill';
    if (contentType.includes('image')) return 'bi-file-earmark-image-fill';
    if (contentType.includes('zip') || contentType.includes('rar')) return 'bi-file-earmark-zip-fill';
    if (contentType.includes('text') || contentType.includes('csv')) return 'bi-file-earmark-text-fill';
    return 'bi-file-earmark-fill';
  }

  getFileIconColor(contentType: string): string {
    if (contentType.includes('pdf')) return 'text-danger';
    if (contentType.includes('word') || contentType.includes('document')) return 'text-primary';
    if (contentType.includes('excel') || contentType.includes('spreadsheet')) return 'text-success';
    if (contentType.includes('powerpoint') || contentType.includes('presentation')) return 'text-warning';
    if (contentType.includes('image')) return 'text-info';
    if (contentType.includes('zip') || contentType.includes('rar')) return 'text-secondary';
    return 'text-muted';
  }

  hasError(field: string, error: string): boolean {
    const ctrl = this.uploadForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isInvalid(field: string): boolean {
    const ctrl = this.uploadForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  // ── Private ─────────────────────────────────────────────────────────────────

  private buildForm(): void {
    this.uploadForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
    });
  }

  private validateAndSetFile(file: File): void {
    this.errorMessage = null;

    if (!ALLOWED_TYPES.includes(file.type)) {
      this.errorMessage = `File type "${file.type || 'unknown'}" is not allowed. Accepted: ${ALLOWED_EXTENSIONS}`;
      this.selectedFile = null;
      return;
    }

    if (file.size > MAX_FILE_SIZE) {
      this.errorMessage = `File size (${this.formatFileSize(file.size)}) exceeds the 20 MB limit.`;
      this.selectedFile = null;
      return;
    }

    this.selectedFile = file;
  }

  private loadDocuments(): void {
    this.isLoading = true;
    this.errorMessage = null;

    const source$ = this.projectId
      ? this.documentService.getByProject(this.projectId)
      : this.actionItemId
        ? this.documentService.getByActionItem(this.actionItemId)
        : null;

    if (!source$) {
      this.isLoading = false;
      return;
    }

    source$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (docs) => {
        const data = (docs as any)?.data ?? docs;
        this.documents = Array.isArray(data) ? data : [];
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to load documents.';
        this.isLoading = false;
      },
    });
  }
}
