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
    // Bootstrap appends the backdrop to <body> (z-index 1050). Left inside the
    // page, the modal is trapped in app-layout's stacking context (z-index 1)
    // and sits under its own backdrop, so it cannot be clicked. Move it to
    // <body> as Bootstrap recommends; Angular keeps its bindings on the node.
    document.body.appendChild(this.modalEl.nativeElement);
    this.bsModal = new bootstrap.Modal(this.modalEl.nativeElement, { backdrop: 'static' });
  }

  ngOnDestroy(): void {
    this.bsModal?.dispose();
    // No longer inside the host element, so Angular will not remove it.
    this.modalEl?.nativeElement.remove();
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
