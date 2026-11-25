import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { TagInputComponent } from './tag-input.component';
import { ActionItemTagService } from '../../../services/action-item-tag.service';
import { PermissionStateService } from '../../../features/permissions/services/permission-state.service';
import { WorkspaceTag } from '../../../models/action-item-tag.model';

// WO-2026-11 A-5: chips + text box, the tag rules, suggestions from the one
// workspace (keyboard and mouse), read-only when disabled.
describe('TagInputComponent (WO-2026-11 A-5)', () => {
  let fixture: ComponentFixture<TagInputComponent>;
  let svc: jasmine.SpyObj<ActionItemTagService>;
  let canView = true;

  const wsTags: WorkspaceTag[] = [
    { id: 't1', name: 'Urgent', itemCount: 3 },
    { id: 't2', name: 'Board pack', itemCount: 1 },
    { id: 't3', name: 'Budget', itemCount: 0 },
  ];

  async function render(inputs: Record<string, unknown> = {}): Promise<HTMLElement> {
    svc = jasmine.createSpyObj<ActionItemTagService>('ActionItemTagService', ['workspaceTags', 'myActionTags']);
    svc.workspaceTags.and.returnValue(of({ success: true, data: wsTags } as any));
    await TestBed.configureTestingModule({
      imports: [TagInputComponent],
      providers: [
        { provide: ActionItemTagService, useValue: svc },
        { provide: PermissionStateService, useValue: { hasPermission: () => canView } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(TagInputComponent);
    fixture.componentRef.setInput('inputId', 'tags');
    fixture.componentRef.setInput('workspaceId', 'ws-1');
    for (const [k, v] of Object.entries(inputs)) fixture.componentRef.setInput(k, v);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => { canView = true; });

  const box = (el: HTMLElement) => el.querySelector<HTMLInputElement>('#tags')!;
  const chips = (el: HTMLElement) => Array.from(el.querySelectorAll('.tgi__chip-name')).map(c => c.textContent!.trim());
  const options = (el: HTMLElement) => Array.from(el.querySelectorAll('[role=option] .tgi__option-name')).map(o => o.textContent!.trim());

  function type(el: HTMLElement, value: string): void {
    box(el).value = value;
    box(el).dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }
  function key(el: HTMLElement, k: string): KeyboardEvent {
    const e = new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true });
    box(el).dispatchEvent(e);
    fixture.detectChanges();
    return e;
  }

  it('Enter adds the cleaned tag; a comma separates tags; duplicates are refused whatever the case', async () => {
    const el = await render();
    type(el, '  Q4   review ');
    expect(key(el, 'Enter').defaultPrevented).toBeTrue();            // never submits the form around it
    type(el, 'alpha, beta,');
    expect(chips(el)).toEqual(['Q4 review', 'alpha', 'beta']);
    expect(box(el).value).toBe('');

    type(el, 'ALPHA');
    key(el, 'Enter');
    expect(chips(el)).toEqual(['Q4 review', 'alpha', 'beta']);
    expect(el.querySelector('[role=alert]')!.textContent).toContain('"ALPHA" is already added.');
    expect(fixture.componentInstance.tags()).toEqual(['Q4 review', 'alpha', 'beta']);
  });

  it('refuses a tag over 30 characters and an eleventh tag, with the server wording', async () => {
    const el = await render({ tags: Array.from({ length: 9 }, (_, i) => `t${i}`) });
    type(el, 'x'.repeat(31));
    key(el, 'Enter');
    expect(el.querySelector('[role=alert]')!.textContent).toContain(`is longer than 30 characters.`);

    type(el, 'tenth');
    key(el, 'Enter');
    expect(el.querySelector('.tgi__count')!.textContent).toContain('10 of 10');
    type(el, 'eleventh');
    key(el, 'Enter');
    expect(el.querySelector('[role=alert]')!.textContent).toContain('An action item can have at most 10 tags.');
    expect(fixture.componentInstance.tags().length).toBe(10);
  });

  it('Backspace in an empty box removes the last chip; each chip has a named remove button', async () => {
    const el = await render({ tags: ['one', 'two'] });
    key(el, 'Backspace');
    expect(chips(el)).toEqual(['one']);

    const remove = el.querySelector<HTMLButtonElement>('.tgi__remove')!;
    expect(remove.getAttribute('aria-label')).toBe('Remove tag one');
    remove.click();
    fixture.detectChanges();
    expect(chips(el)).toEqual([]);
  });

  it('suggests the workspace tags on focus, filtered without regard to case, chosen ones left out', async () => {
    const el = await render({ tags: ['Budget'] });
    box(el).dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(svc.workspaceTags).toHaveBeenCalledOnceWith('ws-1');
    expect(options(el)).toEqual(['Urgent', 'Board pack']);
    expect(box(el).getAttribute('aria-expanded')).toBe('true');

    type(el, 'BOA');
    expect(options(el)).toEqual(['Board pack']);
  });

  it('arrow keys and Enter choose a suggestion; Esc closes the list without reaching the drawer', async () => {
    const el = await render();
    box(el).dispatchEvent(new Event('focus'));
    fixture.detectChanges();

    key(el, 'ArrowDown');
    key(el, 'ArrowDown');
    expect(box(el).getAttribute('aria-activedescendant')).toBe('tags-opt-1');
    key(el, 'Enter');
    expect(chips(el)).toEqual(['Board pack']);

    let reachedDocument = false;
    const listener = () => (reachedDocument = true);
    document.addEventListener('keydown', listener);
    const esc = key(el, 'Escape');
    document.removeEventListener('keydown', listener);
    expect(esc.defaultPrevented).toBeTrue();
    expect(reachedDocument).toBeFalse();
    expect(el.querySelector('[role=listbox]')).toBeNull();
  });

  it('a click on a suggestion adds it; a typed name takes the workspace spelling', async () => {
    const el = await render();
    box(el).dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    (el.querySelector('[role=option]') as HTMLElement).click();
    fixture.detectChanges();
    expect(chips(el)).toEqual(['Urgent']);

    type(el, 'budget');
    key(el, 'Enter');
    expect(chips(el)).toEqual(['Urgent', 'Budget']);
  });

  it('asks for no suggestions without ActionItems.View, but typing still works', async () => {
    canView = false;
    const el = await render();
    box(el).dispatchEvent(new Event('focus'));
    type(el, 'free text');
    key(el, 'Enter');
    expect(svc.workspaceTags).not.toHaveBeenCalled();
    expect(chips(el)).toEqual(['free text']);
  });

  it('disabled shows the tags read-only with the note', async () => {
    const el = await render({ tags: ['kept'], disabled: true, lockedNote: 'Tags cannot be changed on a completed action item.' });
    expect(box(el)).toBeNull();
    expect(el.querySelector('.tgi__remove')).toBeNull();
    expect(el.textContent).toContain('kept');
    expect(el.querySelector('.tgi__note')!.textContent).toContain('cannot be changed');
  });
});
