import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { ActionUpdatesSectionComponent } from './action-updates-section.component';
import { ActionUpdateService } from '../../../../services/action-update.service';
import { PermissionStateService } from '../../../permissions/services/permission-state.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ActionUpdate, ActionUpdateList } from '../../../../models/action-update.model';
import { DocumentService } from '../../../../core/services/document.service';
import { isShownHere } from '../../../../core/http/server-reason';

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

const draft: ActionUpdate = {
  id: 'd1', actionItemId: 'item-1', subItemId: null, content: 'My draft text', authorUserId: 'me',
  authorName: 'Mona Me', isHighImportance: false, status: 'draft', isDraft: true, submittedAt: null,
  createdAt: '2026-11-25T08:00:00Z', updatedAt: null, canEdit: true, documentCount: 0,
};
const submitted: ActionUpdate = {
  id: 's1', actionItemId: 'item-1', subItemId: null, content: 'Shared text', authorUserId: 'other',
  authorName: 'Omar Other', isHighImportance: true, status: 'submitted', isDraft: false,
  submittedAt: '2026-11-24T08:00:00Z', createdAt: '2026-11-24T08:00:00Z', updatedAt: null, canEdit: false, documentCount: 2,
};

function listOf(items: ActionUpdate[], subItemId: string | null = null): ActionUpdateList {
  return {
    actionItemId: 'item-1', subItemId, items,
    draftCount: items.filter(i => i.isDraft).length, submittedCount: items.filter(i => !i.isDraft).length,
  };
}

// WO-2026-11 A-3: Save draft / Submit (with confirmation), Draft badge, drafts
// editable by their author only, submitted updates read-only, permissions.
describe('ActionUpdatesSectionComponent (WO-2026-11 A-3)', () => {
  let fixture: ComponentFixture<ActionUpdatesSectionComponent>;
  let svc: jasmine.SpyObj<ActionUpdateService>;
  let toast: jasmine.SpyObj<ToastService>;
  let docs: jasmine.SpyObj<DocumentService>;
  let granted: Set<string>;

  async function render(items: ActionUpdate[], opts: { subItemId?: string; perms?: string[] } = {}): Promise<HTMLElement> {
    granted = new Set(opts.perms ?? ['ActionItems.View', 'ActionItems.Edit']);
    svc.list.and.returnValue(of({ success: true, data: listOf(items, opts.subItemId ?? null) } as any));

    await TestBed.configureTestingModule({
      imports: [ActionUpdatesSectionComponent],
      providers: [
        { provide: ActionUpdateService, useValue: svc },
        { provide: ToastService, useValue: toast },
        { provide: DocumentService, useValue: docs },
        {
          provide: PermissionStateService,
          useValue: {
            permissions$: new BehaviorSubject<unknown[]>([]),
            hasPermission: (area: string, action: string) => granted.has(`${area}.${action}`),
          },
        },
      ],
    })
      .overrideComponent(ActionUpdatesSectionComponent, {
        remove: { imports: [ConfirmDialogComponent] },
        add: { imports: [StubConfirmDialogComponent] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(ActionUpdatesSectionComponent);
    fixture.componentRef.setInput('actionItemId', 'item-1');
    if (opts.subItemId) {
      fixture.componentRef.setInput('subItemId', opts.subItemId);
      fixture.componentRef.setInput('subItemTitle', 'Draft the plan');
      fixture.componentRef.setInput('compact', true);
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function type(el: HTMLElement, text: string): void {
    const area = el.querySelector<HTMLTextAreaElement>('#au-item-new')!;
    area.value = text;
    area.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function button(scope: ParentNode, label: string): HTMLButtonElement | undefined {
    return Array.from(scope.querySelectorAll<HTMLButtonElement>('button'))
      .find(b => b.textContent!.replace(/\s+/g, ' ').trim().startsWith(label));
  }

  function dialogs(): StubConfirmDialogComponent[] {
    return fixture.debugElement.queryAll(d => d.componentInstance instanceof StubConfirmDialogComponent)
      .map(d => d.componentInstance as StubConfirmDialogComponent);
  }

  beforeEach(() => {
    svc = jasmine.createSpyObj<ActionUpdateService>('ActionUpdateService',
      ['list', 'counts', 'create', 'update', 'submit', 'delete']);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info', 'warning']);
    docs = jasmine.createSpyObj<DocumentService>('DocumentService', ['getByEntity', 'upload', 'download', 'delete']);
    docs.getByEntity.and.returnValue(of({ success: true, data: [] } as any));
  });

  it('shows the count, the Draft badge, and draft buttons only on the own draft', async () => {
    const el = await render([draft, submitted]);

    expect(el.querySelector('#au-item-heading')!.textContent).toContain('Action Updates (2)');
    const [draftEntry, submittedEntry] = Array.from(el.querySelectorAll<HTMLElement>('.au-entry'));
    expect(draftEntry.textContent).toContain('Draft');
    expect(button(draftEntry, 'Edit')).toBeDefined();
    expect(button(draftEntry, 'Submit')).toBeDefined();
    expect(button(draftEntry, 'Delete')).toBeDefined();

    expect(submittedEntry.textContent).toContain('High importance');
    expect(submittedEntry.querySelectorAll('.au-entry__actions button').length).toBe(0);
    expect(submittedEntry.querySelector('.au-files-toggle')!.textContent).toContain('(2)');
    expect(submittedEntry.textContent).not.toContain('Draft');
  });

  it('Save draft sends submit: false; Submit asks first and sends submit: true only when confirmed', async () => {
    const el = await render([]);
    svc.create.and.callFake((_a, _s, body) => of({ success: true, data: {
      ...draft, id: body.submit ? 'n2' : 'n1', content: body.content,
      status: body.submit ? 'submitted' : 'draft', isDraft: !body.submit, canEdit: !body.submit,
    } } as any));

    expect(button(el, 'Save draft')!.disabled).toBeTrue();
    type(el, '   ');
    expect(button(el, 'Save draft')!.disabled).toBeTrue();

    type(el, '  First draft  ');
    button(el, 'Save draft')!.click();
    expect(svc.create).toHaveBeenCalledWith('item-1', null, { content: 'First draft', isHighImportance: false, submit: false });

    type(el, 'Ready to share');
    button(el, 'Submit')!.click();
    expect(svc.create).toHaveBeenCalledTimes(1);
    const submitDialog = dialogs()[0];
    expect(submitDialog.opened).toBe(1);
    expect(submitDialog.message()).toContain('cannot be changed or deleted afterwards');

    fixture.componentInstance.onSubmitConfirmed(false);
    expect(svc.create).toHaveBeenCalledTimes(1);

    button(el, 'Submit')!.click();
    fixture.componentInstance.onSubmitConfirmed(true);
    expect(svc.create).toHaveBeenCalledWith('item-1', null, { content: 'Ready to share', isHighImportance: false, submit: true });
    fixture.detectChanges();
    expect(el.querySelector('#au-item-heading')!.textContent).toContain('Action Updates (2)');
  });

  it('submitting a draft goes through the confirm dialog and leaves it read-only', async () => {
    const el = await render([draft]);
    svc.submit.and.returnValue(of({ success: true, data: {
      ...draft, status: 'submitted', isDraft: false, canEdit: false, submittedAt: '2026-11-25T09:00:00Z',
    } } as any));

    button(el.querySelector('.au-entry')!, 'Submit')!.click();
    expect(svc.submit).not.toHaveBeenCalled();
    expect(dialogs()[0].opened).toBe(1);

    fixture.componentInstance.onSubmitConfirmed(true);
    fixture.detectChanges();
    expect(svc.submit).toHaveBeenCalledWith('item-1', null, 'd1');
    const entry = el.querySelector<HTMLElement>('.au-entry')!;
    expect(entry.textContent).not.toContain('Draft');
    expect(entry.querySelectorAll('button').length).toBe(0);
  });

  it('edits and deletes a draft (delete after confirmation)', async () => {
    const el = await render([draft]);
    svc.update.and.returnValue(of({ success: true, data: { ...draft, content: 'Edited', updatedAt: '2026-11-25T09:00:00Z' } } as any));
    svc.delete.and.returnValue(of(void 0));

    button(el.querySelector('.au-entry')!, 'Edit')!.click();
    fixture.detectChanges();
    const area = el.querySelector<HTMLTextAreaElement>('#au-item-edit-d1')!;
    area.value = ' Edited ';
    area.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    button(el.querySelector('.au-entry')!, 'Save draft')!.click();
    fixture.detectChanges();
    expect(svc.update).toHaveBeenCalledWith('item-1', null, 'd1', { content: 'Edited', isHighImportance: false });
    expect(el.querySelector('.au-text')!.textContent).toBe('Edited');

    button(el.querySelector('.au-entry')!, 'Delete')!.click();
    expect(dialogs()[1].opened).toBe(1);
    fixture.componentInstance.onDeleteConfirmed(true);
    fixture.detectChanges();
    expect(svc.delete).toHaveBeenCalledWith('item-1', null, 'd1');
    expect(el.querySelectorAll('.au-entry').length).toBe(0);
    expect(el.textContent).toContain('No action updates yet.');
  });

  it('a 409 (already submitted) shows the reason and reloads', async () => {
    const el = await render([draft]);
    svc.submit.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { message: 'This action update has been submitted. A submitted update cannot be changed or deleted.' },
    })));

    button(el.querySelector('.au-entry')!, 'Submit')!.click();
    fixture.componentInstance.onSubmitConfirmed(true);

    expect(toast.error).toHaveBeenCalledWith(jasmine.stringContaining('cannot be changed or deleted'));
    expect(svc.list).toHaveBeenCalledTimes(2);
  });

  it('without ActionItems.Edit there is no form and no draft buttons', async () => {
    const el = await render([draft, submitted], { perms: ['ActionItems.View'] });

    expect(el.querySelector('textarea')).toBeNull();
    expect(el.querySelectorAll('.au-entry__actions button').length).toBe(0);
    expect(el.querySelectorAll('.au-entry').length).toBe(2);
  });

  it('without ActionItems.View nothing is shown or loaded', async () => {
    const el = await render([draft], { perms: [] });

    expect(el.querySelector('section')).toBeNull();
    expect(svc.list).not.toHaveBeenCalled();
  });

  it('at sub-item level it loads that sub-item and reports its count', async () => {
    const counts: number[] = [];
    const el = await render([{ ...submitted, subItemId: 'sub-1' }], { subItemId: 'sub-1' });
    fixture.componentInstance.countChange.subscribe(n => counts.push(n));
    fixture.componentInstance.load();

    expect(svc.list).toHaveBeenCalledWith('item-1', 'sub-1');
    expect(el.querySelector('#au-sub-1-heading')!.textContent).toContain('Updates (1)');
    expect(el.querySelector('#au-sub-1-new')).not.toBeNull();
    expect(counts).toEqual([1]);
  });

  // ── WO-2026-11 A-4: files on updates ─────────────────────────────────────

  /** The create → upload → submit chain is a series of awaits: let it run to the end. */
  async function settle(): Promise<void> {
    for (let i = 0; i < 5; i++) await new Promise(r => setTimeout(r, 0));
    fixture.detectChanges();
  }

  const pdf = (name: string, bytes = 100) => new File([new Uint8Array(bytes)], name, { type: 'application/pdf' });
  function pick(files: File[]): void {
    fixture.componentInstance.addFiles({ target: { files, value: '' } } as unknown as Event);
    fixture.detectChanges();
  }

  it('Submit with attached files creates a draft, uploads each file, then submits', async () => {
    const el = await render([]);
    svc.create.and.returnValue(of({ success: true, data: { ...draft, id: 'n1', content: 'With files' } } as any));
    docs.upload.and.returnValue(of({ success: true, data: {} } as any));
    svc.submit.and.returnValue(of({ success: true, data: { ...draft, id: 'n1', status: 'submitted', isDraft: false, canEdit: false } } as any));

    type(el, 'With files');
    pick([pdf('minutes.pdf'), pdf('plan.docx')]);
    expect(el.querySelectorAll('.au-staged__item').length).toBe(2);

    button(el, 'Submit')!.click();
    fixture.detectChanges();
    expect(dialogs()[0].message()).toContain('with its 2 files');
    fixture.componentInstance.onSubmitConfirmed(true);
    await settle();

    expect(svc.create).toHaveBeenCalledWith('item-1', null, { content: 'With files', isHighImportance: false, submit: false });
    expect(docs.upload).toHaveBeenCalledTimes(2);
    expect(docs.upload.calls.argsFor(0).slice(0, 3)).toEqual(['ActionItemUpdate', 'n1', 'minutes']);
    expect(svc.submit).toHaveBeenCalledWith('item-1', null, 'n1');
    expect(svc.create.calls.mostRecent().args[2].submit).toBeFalse();
    fixture.detectChanges();
    expect(el.querySelectorAll('.au-staged__item').length).toBe(0);
  });

  it('an upload failure keeps the draft and does not submit', async () => {
    const el = await render([]);
    svc.create.and.returnValue(of({ success: true, data: { ...draft, id: 'n1' } } as any));
    const tooBig = new HttpErrorResponse({ status: 400, error: { message: 'File size exceeds the 10 MB limit.' } });
    docs.upload.and.returnValue(throwError(() => tooBig));

    type(el, 'With a bad file');
    pick([pdf('big.pdf')]);
    button(el, 'Submit')!.click();
    fixture.componentInstance.onSubmitConfirmed(true);
    await settle();

    expect(svc.submit).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(jasmine.stringMatching(/saved as a draft, but “big\.pdf” could not be attached: File size exceeds the 10 MB limit\. It was not submitted\./));
    expect(isShownHere(tooBig)).withContext('this toast carries the reason: the error interceptor stays silent').toBeTrue();
    expect(svc.list).toHaveBeenCalledTimes(2);
  });

  it('Save draft without files keeps the single create call', async () => {
    const el = await render([]);
    svc.create.and.returnValue(of({ success: true, data: { ...draft, id: 'n1' } } as any));
    type(el, 'No files');
    button(el, 'Save draft')!.click();
    expect(docs.upload).not.toHaveBeenCalled();
    expect(svc.create).toHaveBeenCalledWith('item-1', null, { content: 'No files', isHighImportance: false, submit: false });
  });

  it('refuses wrong types, files over 10 MB and more than 10 files in the form', async () => {
    const el = await render([]);
    pick([pdf('run.exe'), pdf('huge.pdf', 10 * 1024 * 1024 + 1), ...Array.from({ length: 11 }, (_, i) => pdf(`f${i}.pdf`))]);

    expect(fixture.componentInstance.staged().length).toBe(10);
    const err = el.querySelector('.au-callout')!.textContent!;
    expect(err).toContain('run.exe: only PDF, Word, Excel and PowerPoint files');
    expect(err).toContain('huge.pdf: larger than 10 MB');
    expect(err).toContain('f10.pdf: at most 10 files per update');
  });

  it('shows a Files (n) toggle on updates that have files and on my drafts, and opens their files', async () => {
    const el = await render([draft, submitted]);
    const toggles = Array.from(el.querySelectorAll<HTMLButtonElement>('.au-files-toggle')).map(b => b.textContent!.replace(/\s+/g, ' ').trim());
    expect(toggles).toEqual(['Files of this update (0)', 'Files of this update (2)']);

    el.querySelector<HTMLButtonElement>('#au-item-filesbtn-s1')!.click();
    fixture.detectChanges();
    expect(docs.getByEntity).toHaveBeenCalledWith('ActionItemUpdate', 's1');
    expect(el.querySelector('#au-item-files-s1 app-documents-section')).not.toBeNull();
  });
});

