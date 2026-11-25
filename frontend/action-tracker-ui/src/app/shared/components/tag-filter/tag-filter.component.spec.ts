import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TagFilterComponent } from './tag-filter.component';
import { TagFilterOption } from '../../../models/action-item-tag.model';

// WO-2026-11 A-5: "Tags" filter — options grouped by workspace, any of the
// chosen ids, search, clear, Esc returns focus to the button.
describe('TagFilterComponent (WO-2026-11 A-5)', () => {
  let fixture: ComponentFixture<TagFilterComponent>;

  const opts: TagFilterOption[] = [
    { id: 'a1', name: 'Urgent', group: 'Alpha', count: 2 },
    { id: 'a2', name: 'Board pack', group: 'Alpha', count: 1 },
    { id: 'b1', name: 'Urgent', group: 'Beta', count: 1 },
  ];

  async function render(selected: string[] = []): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [TagFilterComponent] }).compileComponents();
    fixture = TestBed.createComponent(TagFilterComponent);
    fixture.componentRef.setInput('options', opts);
    fixture.componentRef.setInput('selected', selected);
    fixture.componentRef.setInput('idPrefix', 'tf');
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const button = (el: HTMLElement) => el.querySelector<HTMLButtonElement>('.tgf__btn')!;
  function open(el: HTMLElement): void { button(el).click(); fixture.detectChanges(); }

  it('groups the options by workspace and keeps the same name apart', async () => {
    const el = await render();
    expect(button(el).getAttribute('aria-expanded')).toBe('false');
    open(el);
    expect(button(el).getAttribute('aria-expanded')).toBe('true');
    const legends = Array.from(el.querySelectorAll('legend')).map(l => l.textContent!.trim());
    expect(legends).toEqual(['Alpha', 'Beta']);
    expect(el.querySelectorAll('input[type=checkbox]').length).toBe(3);
  });

  it('a checkbox adds or removes its id; the button shows how many are chosen', async () => {
    const el = await render();
    const changes: string[][] = [];
    fixture.componentInstance.selected.subscribe(v => changes.push(v));
    open(el);
    const boxes = el.querySelectorAll<HTMLInputElement>('input[type=checkbox]');
    boxes[2].click();                                     // Beta · Urgent
    fixture.detectChanges();
    boxes[0].click();                                     // Alpha · Urgent
    fixture.detectChanges();
    expect(changes.at(-1)).toEqual(['b1', 'a1']);
    expect(button(el).querySelector('.tgf__badge')!.textContent).toContain('2');
    expect(button(el).title).toBe('Urgent (Alpha), Urgent (Beta)');

    boxes[2].click();
    fixture.detectChanges();
    expect(changes.at(-1)).toEqual(['a1']);
  });

  it('search matches the tag or the workspace name; Clear empties the choice', async () => {
    const el = await render(['a2']);
    open(el);
    const search = el.querySelector<HTMLInputElement>('.tgf__search')!;
    search.value = 'BOARD';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el.querySelectorAll('input[type=checkbox]').length).toBe(1);
    search.value = 'beta';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(Array.from(el.querySelectorAll('legend')).map(l => l.textContent!.trim())).toEqual(['Beta']);

    el.querySelector<HTMLButtonElement>('.tgf__clear')!.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.selected()).toEqual([]);
  });

  it('Esc closes the panel and gives focus back to the button', async () => {
    const el = await render();
    document.body.appendChild(el);
    open(el);
    const panel = el.querySelector<HTMLElement>('.tgf__panel')!;
    const esc = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    panel.dispatchEvent(esc);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(esc.defaultPrevented).toBeTrue();
    expect(el.querySelector('.tgf__panel')).toBeNull();
    expect(document.activeElement).toBe(button(el));
    el.remove();
  });

  it('says so when there are no tags', async () => {
    await TestBed.configureTestingModule({ imports: [TagFilterComponent] }).compileComponents();
    fixture = TestBed.createComponent(TagFilterComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    open(el);
    expect(el.textContent).toContain('No tags on these action items yet.');
  });
});
