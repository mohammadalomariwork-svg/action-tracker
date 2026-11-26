import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, of, throwError } from 'rxjs';

import { ProjectClassificationsSectionComponent } from './project-classifications-section.component';
import { ProjectClassificationService } from '../../../../../services/project-classification.service';
import { PermissionStateService } from '../../../../permissions/services/permission-state.service';
import { ConfirmDialogComponent } from '../../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ProjectClassification } from '../../../../../models/project-classification.model';

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

function c(id: string, name: string, sortOrder: number, extra: Partial<ProjectClassification> = {}): ProjectClassification {
  return {
    id, workspaceId: 'ws-1', name, sortOrder, isActive: true, projectCount: 0, deletedProjectCount: 0,
    canDelete: true, ...extra,
  };
}

// WO-2026-11 P-1: Workspace Settings → Project Classifications — list, add with
// the name rules, rename, reorder, deactivate, delete (confirmed, blocked while
// used), and nothing to change without Workspaces.Edit.
describe('ProjectClassificationsSectionComponent (WO-2026-11 P-1)', () => {
  let fixture: ComponentFixture<ProjectClassificationsSectionComponent>;
  let svc: jasmine.SpyObj<ProjectClassificationService>;
  let granted: Set<string>;

  const capital = c('a', 'Capital works', 1, { projectCount: 2, canDelete: false });
  const research = c('b', 'Research', 2);
  const retired = c('d', 'Retired', 3, { isActive: false, deletedProjectCount: 1, canDelete: false });

  async function render(list: ProjectClassification[], perms = ['Workspaces.View', 'Workspaces.Edit']): Promise<HTMLElement> {
    granted = new Set(perms);
    svc.list.and.returnValue(of({ success: true, data: list } as any));
    await TestBed.configureTestingModule({
      imports: [ProjectClassificationsSectionComponent],
      providers: [
        { provide: ProjectClassificationService, useValue: svc },
        {
          provide: PermissionStateService,
          useValue: {
            permissions$: new BehaviorSubject<unknown[]>([]),
            hasPermission: (area: string, action: string) => granted.has(`${area}.${action}`),
          },
        },
      ],
    })
      .overrideComponent(ProjectClassificationsSectionComponent, {
        remove: { imports: [ConfirmDialogComponent] },
        add: { imports: [StubConfirmDialogComponent] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(ProjectClassificationsSectionComponent);
    fixture.componentRef.setInput('workspaceId', 'ws-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function names(el: HTMLElement): string[] {
    return Array.from(el.querySelectorAll('.pcs__name')).map(n => n.textContent!.trim());
  }

  function dialog(): StubConfirmDialogComponent {
    return fixture.debugElement.query(d => d.componentInstance instanceof StubConfirmDialogComponent)
      .componentInstance as StubConfirmDialogComponent;
  }

  function typeNew(el: HTMLElement, value: string): void {
    const input = el.querySelector<HTMLInputElement>('#pcsNewName')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    svc = jasmine.createSpyObj<ProjectClassificationService>('ProjectClassificationService',
      ['list', 'create', 'update', 'reorder', 'delete']);
  });

  it('lists the classifications in order with Inactive badges and usage', async () => {
    const el = await render([capital, research, retired]);

    expect(names(el)).toEqual(['Capital works', 'Research', 'Retired']);
    expect(el.querySelectorAll('.pcs__badge').length).toBe(1);
    const usage = Array.from(el.querySelectorAll('.pcs__usage')).map(u => u.textContent!.trim());
    expect(usage).toEqual([
      'Used by 2 projects — cannot be deleted',
      'Not used by any project',
      'Used by 1 project (deleted) — cannot be deleted',
    ]);
  });

  it('shows no add form and no row buttons without Workspaces.Edit', async () => {
    const el = await render([capital, research], ['Workspaces.View']);

    expect(el.querySelector('#pcsNewName')).toBeNull();
    expect(el.querySelector('.pcs__actions')).toBeNull();
    expect(names(el)).toEqual(['Capital works', 'Research']);
  });

  it('refuses an empty name or a duplicate in any case before calling the server', async () => {
    const el = await render([capital]);

    typeNew(el, '   ');
    el.querySelector<HTMLFormElement>('.pcs__add')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(el.querySelector('#pcsNewNameProblem')!.textContent).toContain('Enter a name');

    typeNew(el, ' CAPITAL  works ');
    el.querySelector<HTMLFormElement>('.pcs__add')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(el.querySelector('#pcsNewNameProblem')!.textContent).toContain('already has a classification named');
    expect(svc.create).not.toHaveBeenCalled();
  });

  it('adds a cleaned name and appends the result', async () => {
    const el = await render([capital]);
    svc.create.and.returnValue(of({ success: true, data: c('n', 'Research grants', 2) } as any));

    typeNew(el, '  Research   grants ');
    el.querySelector<HTMLFormElement>('.pcs__add')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(svc.create).toHaveBeenCalledWith('ws-1', { name: 'Research grants' });
    expect(names(el)).toEqual(['Capital works', 'Research grants']);
    expect(el.querySelector<HTMLInputElement>('#pcsNewName')!.value).toBe('');
  });

  it('shows the server reason when an add is refused', async () => {
    const el = await render([capital]);
    svc.create.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { success: false, message: 'This workspace already has a classification named "X".' },
    })));

    typeNew(el, 'X');
    el.querySelector<HTMLFormElement>('.pcs__add')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(el.querySelector('#pcsNewNameProblem')!.textContent).toContain('already has a classification named "X"');
  });

  it('renames inline: Enter saves, Escape cancels', async () => {
    const el = await render([capital, research]);
    svc.update.and.returnValue(of({ success: true, data: { ...research, name: 'Research grants' } } as any));

    el.querySelector<HTMLButtonElement>('#pcsRename-b')!.click();
    fixture.detectChanges();
    const input = el.querySelector<HTMLInputElement>('#pcsRenameInput-b')!;
    input.value = 'Research grants';
    input.dispatchEvent(new Event('input'));
    input.closest('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(svc.update).toHaveBeenCalledWith('ws-1', 'b', { name: 'Research grants', isActive: true });
    expect(names(el)).toEqual(['Capital works', 'Research grants']);

    el.querySelector<HTMLButtonElement>('#pcsRename-a')!.click();
    fixture.detectChanges();
    el.querySelector<HTMLInputElement>('#pcsRenameInput-a')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(el.querySelector('#pcsRenameInput-a')).toBeNull();
    expect(svc.update).toHaveBeenCalledTimes(1);
  });

  it('moves a classification down with the full new order', async () => {
    const el = await render([capital, research, retired]);
    svc.reorder.and.returnValue(of({ success: true, data: [research, capital, retired] } as any));

    expect(el.querySelector<HTMLButtonElement>('#pcsUp-a')!.disabled).toBeTrue();
    expect(el.querySelector<HTMLButtonElement>('#pcsDown-d')!.disabled).toBeTrue();
    el.querySelector<HTMLButtonElement>('#pcsDown-a')!.click();
    fixture.detectChanges();

    expect(svc.reorder).toHaveBeenCalledWith('ws-1', ['b', 'a', 'd']);
    expect(names(el)).toEqual(['Research', 'Capital works', 'Retired']);
  });

  it('deactivates and reactivates through the update call', async () => {
    const el = await render([research]);
    svc.update.and.returnValue(of({ success: true, data: { ...research, isActive: false } } as any));

    el.querySelector<HTMLButtonElement>('#pcsToggle-b')!.click();
    fixture.detectChanges();

    expect(svc.update).toHaveBeenCalledWith('ws-1', 'b', { name: 'Research', isActive: false });
    expect(el.querySelector('#pcsToggle-b')!.textContent).toContain('Activate');
    expect(el.querySelectorAll('.pcs__badge').length).toBe(1);
  });

  it('disables delete while projects use it, and deletes only after confirming', async () => {
    const el = await render([capital, research]);
    svc.delete.and.returnValue(of(undefined));

    expect(el.querySelector<HTMLButtonElement>('#pcsDelete-a')!.disabled).toBeTrue();

    el.querySelector<HTMLButtonElement>('#pcsDelete-b')!.click();
    fixture.detectChanges();
    expect(dialog().opened).toBe(1);
    expect(svc.delete).not.toHaveBeenCalled();

    // Cancel: nothing happens.
    dialog().confirmed.emit(false);
    expect(svc.delete).not.toHaveBeenCalled();

    el.querySelector<HTMLButtonElement>('#pcsDelete-b')!.click();
    svc.list.and.returnValue(of({ success: true, data: [capital] } as any));
    dialog().confirmed.emit(true);
    fixture.detectChanges();

    expect(svc.delete).toHaveBeenCalledWith('ws-1', 'b');
    expect(names(el)).toEqual(['Capital works']);
  });

  it('shows the 409 reason of a refused delete and reloads the counts', async () => {
    const el = await render([research]);
    svc.delete.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: { success: false, message: '"Research" is used by 1 project and cannot be deleted.' },
    })));

    el.querySelector<HTMLButtonElement>('#pcsDelete-b')!.click();
    svc.list.and.returnValue(of({ success: true, data: [{ ...research, projectCount: 1, canDelete: false }] } as any));
    dialog().confirmed.emit(true);
    fixture.detectChanges();

    expect(el.querySelector('.pcs__alert')!.textContent).toContain('is used by 1 project');
    expect(el.querySelector<HTMLButtonElement>('#pcsDelete-b')!.disabled).toBeTrue();
  });
});
