import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { DocumentsSectionComponent } from './documents-section.component';
import { DocumentService } from '../../../core/services/document.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { DocumentInfo } from '../../../core/models/document.model';

/** Stand-in for the Bootstrap modal (the real one needs Bootstrap's global JS). */
@Component({ selector: 'app-confirm-dialog', standalone: true, template: '' })
class StubConfirmDialogComponent {
  readonly title = input('');
  readonly message = input('');
  readonly confirmLabel = input('');
  readonly cancelLabel = input('');
  readonly danger = input(false);
  readonly confirmed = output<boolean>();
  opened = 0;
  open(): void { this.opened++; }
}

const doc = (id: string, canDelete?: boolean): DocumentInfo => ({
  id, name: 'Doc ' + id, fileName: id + '.pdf', contentType: 'application/pdf', fileSize: 2048,
  relatedEntityType: 'ActionItem', relatedEntityId: 'item-1', uploadedByUserId: 'u', uploadedByName: 'Una Uploader',
  createdAt: '2026-11-25T08:00:00Z', canDelete,
});

// WO-2026-11 A-4: the optional inputs keep other pages unchanged; the delete
// button follows canDelete and asks first; locked note; cap.
describe('DocumentsSectionComponent (WO-2026-11 A-4)', () => {
  let fixture: ComponentFixture<DocumentsSectionComponent>;
  let docs: jasmine.SpyObj<DocumentService>;

  async function render(list: DocumentInfo[], inputs: Record<string, unknown> = {}): Promise<HTMLElement> {
    docs = jasmine.createSpyObj<DocumentService>('DocumentService', ['getByEntity', 'upload', 'download', 'delete']);
    docs.getByEntity.and.returnValue(of({ success: true, data: list } as any));
    docs.delete.and.returnValue(of(void 0));
    await TestBed.configureTestingModule({
      imports: [DocumentsSectionComponent],
      providers: [
        { provide: DocumentService, useValue: docs },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error']) },
      ],
    })
      .overrideComponent(DocumentsSectionComponent, {
        remove: { imports: [ConfirmDialogComponent] },
        add: { imports: [StubConfirmDialogComponent] },
      })
      .compileComponents();
    fixture = TestBed.createComponent(DocumentsSectionComponent);
    fixture.componentRef.setInput('entityType', 'ActionItem');
    fixture.componentRef.setInput('entityId', 'item-1');
    for (const [k, v] of Object.entries(inputs)) fixture.componentRef.setInput(k, v);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const deleteButtons = (el: HTMLElement) => el.querySelectorAll('.doc-action-btn--delete');

  it('defaults: upload form shown, delete shown when the server sends no flag (other pages)', async () => {
    const el = await render([doc('a')]);
    expect(el.querySelector('.doc-upload-form')).not.toBeNull();
    expect(deleteButtons(el).length).toBe(1);
    expect(el.querySelector('.doc-cap')).toBeNull();
  });

  it('shows the delete button only where canDelete is true', async () => {
    const el = await render([doc('mine', true), doc('theirs', false)]);
    expect(deleteButtons(el).length).toBe(1);
    expect(deleteButtons(el)[0].getAttribute('aria-label')).toBe('Delete Doc mine');
  });

  it('asks before deleting and reports the new count', async () => {
    const el = await render([doc('a', true), doc('b', true)]);
    const counts: number[] = [];
    fixture.componentInstance.countChange.subscribe(n => counts.push(n));

    (deleteButtons(el)[0] as HTMLButtonElement).click();
    const dialog = fixture.debugElement.query(d => d.componentInstance instanceof StubConfirmDialogComponent)
      .componentInstance as StubConfirmDialogComponent;
    expect(dialog.opened).toBe(1);
    expect(docs.delete).not.toHaveBeenCalled();

    fixture.componentInstance.onDeleteConfirmed(true);
    fixture.detectChanges();
    expect(docs.delete).toHaveBeenCalledWith('a');
    expect(counts).toEqual([1]);
    expect(el.querySelector('.card__title')!.textContent).toContain('(1)');
  });

  it('a locked note replaces the upload form', async () => {
    const el = await render([doc('a', false)], { lockedNote: 'This action item is Done.', maxFiles: 10 });
    expect(el.querySelector('.doc-upload-form')).toBeNull();
    expect(el.querySelector('.doc-locked')!.textContent).toContain('This action item is Done.');
    expect(el.querySelector('.doc-cap')!.textContent).toContain('1 of 10 files');
  });

  it('canUpload false hides the form; at the cap the form gives way to a note', async () => {
    let el = await render([doc('a')], { canUpload: false });
    expect(el.querySelector('.doc-upload-form')).toBeNull();

    TestBed.resetTestingModule();
    el = await render(Array.from({ length: 10 }, (_, i) => doc('d' + i, true)), { maxFiles: 10 });
    expect(el.querySelector('.doc-upload-form')).toBeNull();
    expect(el.querySelector('.doc-locked')!.textContent).toContain('maximum of 10 files');
  });
});
