import {
  Component,
  ChangeDetectionStrategy,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

/**
 * Reusable justification dialog for plan submit / revision requests.
 * Mirrors the dialog-backdrop pattern used by plan-form-dialog.
 */
@Component({
  selector: 'app-justification-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './justification-dialog.component.html',
  styleUrl: './justification-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JustificationDialogComponent {
  @Input() title = 'Submit for Approval';
  @Input() submitLabel = 'Submit';
  @Input() errorMessage: string | null = null;
  @Input() submitting = false;

  @Output() cancel = new EventEmitter<void>();
  @Output() submit = new EventEmitter<string>();

  readonly justification = signal('');

  isValid(): boolean {
    return this.justification().trim().length > 0
        && this.justification().trim().length <= 2000;
  }

  onSubmit(): void {
    if (!this.isValid()) return;
    this.submit.emit(this.justification().trim());
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.cancel.emit();
    }
  }
}
