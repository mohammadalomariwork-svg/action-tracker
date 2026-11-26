import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { of } from 'rxjs';

import { CurrentProjectClassification, ProjectClassificationSelectComponent } from './project-classification-select.component';
import { ProjectClassificationService } from '../../../services/project-classification.service';
import { ProjectClassification } from '../../../models/project-classification.model';

function c(id: string, name: string, isActive = true, sortOrder = 1): ProjectClassification {
  return { id, workspaceId: 'ws-1', name, sortOrder, isActive, projectCount: 0, deletedProjectCount: 0, canDelete: true };
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, ProjectClassificationSelectComponent],
  template: `
    <app-project-classification-select inputId="pcs-test" [formControl]="control"
      [workspaceId]="workspaceId()" [current]="current()" />
  `,
})
class HostComponent {
  readonly control = new FormControl<string | null>(null);
  readonly workspaceId = signal<string | null>('ws-1');
  readonly current = signal<CurrentProjectClassification | null>(null);
}

// WO-2026-11 P-1: "None" + active options in order, the project's own inactive
// classification kept as "(inactive)", a form control in both directions.
describe('ProjectClassificationSelectComponent (WO-2026-11 P-1)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let svc: jasmine.SpyObj<ProjectClassificationService>;

  async function render(list: ProjectClassification[], setup?: (host: HostComponent) => void): Promise<HTMLSelectElement> {
    svc = jasmine.createSpyObj<ProjectClassificationService>('ProjectClassificationService', ['options']);
    svc.options.and.returnValue(of(list));
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [{ provide: ProjectClassificationService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    setup?.(fixture.componentInstance);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement.querySelector('select') as HTMLSelectElement;
  }

  function labels(select: HTMLSelectElement): string[] {
    return Array.from(select.options).map(o => o.textContent!.trim());
  }

  it('offers None and the active classifications, with a label tied to the select', async () => {
    const select = await render([c('a', 'Capital works', true, 1), c('b', 'Retired', false, 2), c('d', 'Research', true, 3)]);

    expect(labels(select)).toEqual(['None', 'Capital works', 'Research']);
    expect(svc.options).toHaveBeenCalledWith('ws-1');
    const label = fixture.nativeElement.querySelector('label') as HTMLLabelElement;
    expect(label.htmlFor).toBe('pcs-test');
    expect(label.textContent).toContain('Internal classification');
  });

  it('keeps the project\'s own inactive classification as an option, marked inactive', async () => {
    const select = await render([c('a', 'Capital works'), c('b', 'Retired', false)], host => {
      host.current.set({ id: 'b', name: 'Retired', isActive: false });
      host.control.setValue('b');
    });

    expect(labels(select)).toEqual(['None', 'Capital works', 'Retired (inactive)']);
    expect(select.value).toBe('b');
  });

  it('writes the chosen id (or null for None) to the form control', async () => {
    const select = await render([c('a', 'Capital works')]);

    select.value = 'a';
    select.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.control.value).toBe('a');

    select.value = '';
    select.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.control.value).toBeNull();
  });

  it('says so when the workspace has no classifications, and is disabled without a workspace', async () => {
    const select = await render([]);
    expect(fixture.nativeElement.textContent).toContain('This workspace has no internal classifications');

    fixture.componentInstance.workspaceId.set(null);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(select.disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Choose the workspace first.');
  });

  it('follows the form control\'s disabled state', async () => {
    const select = await render([c('a', 'Capital works')]);
    fixture.componentInstance.control.disable();
    fixture.detectChanges();
    expect(select.disabled).toBeTrue();
  });
});
