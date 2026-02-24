import { Component, input, output, signal, ElementRef, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';

// Bootstrap's JS is loaded globally via angular.json scripts
declare const bootstrap: any;

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
})
export class ConfirmDialogComponent implements AfterViewInit, OnDestroy {
  readonly title = input<string>('Confirm');
  readonly message = input<string>('Are you sure you want to proceed?');
  readonly confirmLabel = input<string>('Confirm');
  readonly cancelLabel = input<string>('Cancel');
  readonly danger = input<boolean>(false);

  readonly confirmed = output<boolean>();

  @ViewChild('modalEl') modalEl!: ElementRef<HTMLElement>;

  private bsModal: any;

  ngAfterViewInit(): void {
    this.bsModal = new bootstrap.Modal(this.modalEl.nativeElement, { backdrop: 'static' });
  }

  ngOnDestroy(): void {
    this.bsModal?.dispose();
  }

  open(): void {
    this.bsModal?.show();
  }

  close(): void {
    this.bsModal?.hide();
  }

  onConfirm(): void {
    this.confirmed.emit(true);
    this.close();
  }

  onCancel(): void {
    this.confirmed.emit(false);
    this.close();
  }
}
