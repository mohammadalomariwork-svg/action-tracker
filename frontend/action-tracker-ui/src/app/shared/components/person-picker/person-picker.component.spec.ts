import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';

import { PersonPickerComponent } from './person-picker.component';
import { PersonOption } from '../../../models/project-people.model';

const ana:  PersonOption = { userId: 'u-ana',  fullName: 'Ana Ahmed',  email: 'ana@ku.ac.ae' };
const bilal: PersonOption = { userId: 'u-bil', fullName: 'Bilal Bakr', email: 'bilal@ku.ac.ae', orgUnitName: 'Digital Services' };
const carla: PersonOption = { userId: 'u-car', fullName: 'Carla Cruz', email: 'carla@ku.ac.ae' };

function key(el: HTMLElement, k: string): void {
  el.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true }));
}

// WO-2026-11 P-2: chips + combobox for the project people pickers.
describe('PersonPickerComponent (WO-2026-11 P-2)', () => {
  let fixture: ComponentFixture<PersonPickerComponent>;
  let host: HTMLElement;

  function box(): HTMLInputElement { return host.querySelector('input[role="combobox"]') as HTMLInputElement; }
  function options(): string[] {
    return [...host.querySelectorAll('[role="option"] .ppk__option-name')].map(o => o.textContent!.trim());
  }
  function chips(): string[] {
    return [...host.querySelectorAll('.ppk__chip-name')].map(c => c.textContent!.trim());
  }
  function type(text: string): void {
    box().value = text;
    box().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [PersonPickerComponent] }).compileComponents();
    fixture = TestBed.createComponent(PersonPickerComponent);
    host = fixture.nativeElement;
    fixture.componentRef.setInput('inputId', 'pp-test');
    fixture.componentRef.setInput('label', 'Viewers');
  });

  it('filters a local list as you type and adds the highlighted person with the keyboard', () => {
    fixture.componentRef.setInput('options', [ana, bilal, carla]);
    fixture.detectChanges();

    box().dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(options()).toEqual(['Ana Ahmed', 'Bilal Bakr', 'Carla Cruz']);
    expect(box().getAttribute('aria-expanded')).toBe('true');

    type('bil');
    expect(options()).toEqual(['Bilal Bakr']);

    key(box(), 'ArrowDown');
    fixture.detectChanges();
    expect(box().getAttribute('aria-activedescendant')).toBe('pp-test-opt-0');
    key(box(), 'Enter');
    fixture.detectChanges();

    expect(chips()).toEqual(['Bilal Bakr']);
    expect(fixture.componentInstance.selected().map(p => p.userId)).toEqual(['u-bil']);
    expect(box().value).toBe('');
  });

  it('never offers a chosen or excluded person', () => {
    fixture.componentRef.setInput('options', [ana, bilal, carla]);
    fixture.componentRef.setInput('exclude', ['U-ANA']);     // ids compared without regard to case
    fixture.componentInstance.selected.set([carla]);
    fixture.detectChanges();
    box().dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    expect(options()).toEqual(['Bilal Bakr']);
  });

  it('removes a chip with its button and the last chip with Backspace in an empty box', () => {
    fixture.componentInstance.selected.set([ana, bilal, carla]);
    fixture.detectChanges();

    const remove = host.querySelector('button[aria-label="Remove Bilal Bakr from Viewers"]') as HTMLButtonElement;
    expect(remove).not.toBeNull();
    remove.click();
    fixture.detectChanges();
    expect(chips()).toEqual(['Ana Ahmed', 'Carla Cruz']);

    key(box(), 'Backspace');
    fixture.detectChanges();
    expect(chips()).toEqual(['Ana Ahmed']);
    expect(host.querySelector('[aria-live="polite"]')!.textContent).toContain('Carla Cruz removed.');
  });

  it('searches remotely only after two characters, debounced', fakeAsync(() => {
    const search = jasmine.createSpy('search').and.callFake((term: string) =>
      of([ana, bilal, carla].filter(p => p.fullName.toLowerCase().includes(term.toLowerCase()))));
    fixture.componentRef.setInput('search', search);
    fixture.detectChanges();

    box().dispatchEvent(new Event('focus'));
    type('c');
    tick(300);
    fixture.detectChanges();
    expect(search).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Type at least 2 characters to search.');

    type('ca');
    type('car');
    tick(300);
    fixture.detectChanges();
    expect(search).toHaveBeenCalledOnceWith('car');
    expect(options()).toEqual(['Carla Cruz']);

    (host.querySelector('[role="option"]') as HTMLElement).click();
    fixture.detectChanges();
    expect(chips()).toEqual(['Carla Cruz']);
  }));

  it('Escape closes the list without letting the key reach a surrounding drawer', () => {
    fixture.componentRef.setInput('options', [ana]);
    fixture.detectChanges();
    box().dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    const outer = jasmine.createSpy('outer');
    host.addEventListener('keydown', outer);
    key(box(), 'Escape');
    fixture.detectChanges();
    expect(host.querySelector('[role="listbox"]')).toBeNull();
    expect(outer).not.toHaveBeenCalled();
  });

  it('stops at max and hides the box', () => {
    fixture.componentRef.setInput('options', [ana, bilal]);
    fixture.componentRef.setInput('max', 1);
    fixture.componentInstance.selected.set([ana]);
    fixture.detectChanges();
    expect(box()).toBeNull();
  });

  it('shows a required error only after a save attempt, linked to the box', () => {
    fixture.componentRef.setInput('required', true);
    fixture.componentRef.setInput('search', () => of([]));
    fixture.detectChanges();
    expect(host.querySelector('.ppk__error')).toBeNull();

    fixture.componentRef.setInput('showErrors', true);
    fixture.detectChanges();
    expect(host.querySelector('.ppk__error')!.textContent).toContain('Choose at least one person.');
    expect(box().getAttribute('aria-invalid')).toBe('true');
    expect(box().getAttribute('aria-describedby')).toContain('pp-test-error');
  });

  it('read-only mode lists the chips without buttons and shows the note', () => {
    fixture.componentInstance.selected.set([ana]);
    fixture.componentRef.setInput('disabled', true);
    fixture.componentRef.setInput('lockedNote', 'Only managers can change this.');
    fixture.detectChanges();
    expect(chips()).toEqual(['Ana Ahmed']);
    expect(host.querySelector('button')).toBeNull();
    expect(box()).toBeNull();
    expect(host.textContent).toContain('Only managers can change this.');
  });
});
