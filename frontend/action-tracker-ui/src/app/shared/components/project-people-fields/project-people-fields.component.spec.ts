import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';

import { ProjectPeopleFieldsComponent } from './project-people-fields.component';
import { ProjectPeopleService } from '../../../services/project-people.service';
import { emptyPeopleValue } from '../../utils/project-people.util';
import { PersonPickerComponent } from '../person-picker/person-picker.component';

// WO-2026-11 P-2: the "People" block of the four project edit screens.
describe('ProjectPeopleFieldsComponent (WO-2026-11 P-2)', () => {
  let fixture: ComponentFixture<ProjectPeopleFieldsComponent>;
  let lookup: jasmine.Spy;

  beforeEach(async () => {
    lookup = jasmine.createSpy('lookup').and.returnValue(of([]));
    await TestBed.configureTestingModule({
      imports: [ProjectPeopleFieldsComponent],
      providers: [{ provide: ProjectPeopleService, useValue: { lookup } }],
    }).compileComponents();
    fixture = TestBed.createComponent(ProjectPeopleFieldsComponent);
    fixture.componentRef.setInput('value', emptyPeopleValue([{ userId: 's1', fullName: 'Sara Sponsor' }]));
    fixture.componentRef.setInput('idPrefix', 'tst');
  });

  function pickers() {
    return fixture.debugElement.queryAll(By.directive(PersonPickerComponent))
      .map(d => d.componentInstance as PersonPickerComponent);
  }

  it('has four pickers; the PM list feeds additional PMs, the lookup the other three; the lead is excluded', () => {
    fixture.componentRef.setInput('pmOptions', [{ userId: 'pm2', fullName: 'Pavel' }, { userId: 'lead', fullName: 'Layla' }]);
    fixture.componentRef.setInput('leadPmUserId', 'lead');
    fixture.detectChanges();
    const [pms, sponsors, viewers, team] = pickers();
    expect([pms.label(), sponsors.label(), viewers.label(), team.label()])
      .toEqual(['Additional project managers', 'Sponsors', 'Viewers', 'Team members']);
    expect(pms.options()!.map(o => o.userId)).toEqual(['pm2', 'lead']);
    expect(pms.exclude()).toEqual(['lead']);
    expect(pms.choices().map(o => o.userId)).toEqual(['pm2']);
    expect(sponsors.options()).toBeNull();
    expect(sponsors.required()).toBeTrue();
    sponsors.search()!('sa');
    expect(lookup).toHaveBeenCalledWith('sa');
  });

  it('writes each picker back into its own list', () => {
    fixture.detectChanges();
    const [, , viewers, team] = pickers();
    viewers.selected.set([{ userId: 'v1', fullName: 'Vera' }]);
    team.selected.set([{ userId: 't1', fullName: 'Tariq' }]);
    fixture.detectChanges();
    const v = fixture.componentInstance.value();
    expect(v.viewers.map(p => p.userId)).toEqual(['v1']);
    expect(v.teamMembers.map(p => p.userId)).toEqual(['t1']);
    expect(v.sponsors.map(p => p.userId)).toEqual(['s1']);
  });

  it('D4: without the right to add PMs, nobody can be added but chips can be removed', () => {
    fixture.componentRef.setInput('pmOptions', [{ userId: 'pm3', fullName: 'Pia' }]);
    fixture.componentRef.setInput('canAddProjectManagers', false);
    fixture.componentRef.setInput('value', { ...emptyPeopleValue(), additionalManagers: [{ userId: 'pm2', fullName: 'Pavel' }] });
    fixture.detectChanges();
    const [pms] = pickers();
    expect(pms.options()).toEqual([]);
    expect(pms.disabled()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain("Only the workspace's admins and members can add project managers.");
    const remove = fixture.nativeElement.querySelector('button[aria-label="Remove Pavel from Additional project managers"]');
    expect(remove).not.toBeNull();
  });
});
