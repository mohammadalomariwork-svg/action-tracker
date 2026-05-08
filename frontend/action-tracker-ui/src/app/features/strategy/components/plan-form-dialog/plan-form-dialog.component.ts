import {
  Component,
  ChangeDetectionStrategy,
  EventEmitter,
  Input,
  Output,
  OnChanges,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { StrategicPlan } from '../../../admin/models/strategic-plans.models';
import { OrgUnitSelectComponent, OrgUnitOption } from '../../../../shared';

@Component({
  selector: 'app-plan-form-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, OrgUnitSelectComponent],
  templateUrl: './plan-form-dialog.component.html',
  styleUrl: './plan-form-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlanFormDialogComponent implements OnChanges {
  @Input() mode: 'create' | 'edit' = 'create';
  @Input() plan: StrategicPlan | null = null;
  @Input() errorMessage: string | null = null;
  /**
   * Whenever this list is non-empty, an org-unit selector is rendered. In
   * `create` mode the parent passes the units the user can create against
   * (no in-flight plan); in `edit` mode the parent passes those same units
   * **plus** the plan's current org unit, so the user can re-assign a Draft
   * plan to a different unit. Leave empty to hide the picker entirely.
   */
  @Input() orgUnitOptions: ReadonlyArray<OrgUnitOption> = [];

  @Output() cancel = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    name: string;
    startYear: number;
    durationYears: number;
    orgUnitId?: string;
  }>();

  readonly name = signal('');
  readonly startYear = signal<number>(new Date().getFullYear());
  readonly durationYears = signal<number>(1);
  readonly orgUnitId = signal<string>('');

  ngOnChanges(): void {
    if (this.mode === 'edit' && this.plan) {
      this.name.set(this.plan.name);
      this.startYear.set(this.plan.startYear);
      this.durationYears.set(this.plan.durationYears);
      this.orgUnitId.set(this.plan.orgUnitId);
    } else if (this.mode === 'create') {
      this.name.set('');
      this.startYear.set(new Date().getFullYear());
      this.durationYears.set(1);
      this.orgUnitId.set(this.orgUnitOptions.length === 1 ? this.orgUnitOptions[0].id : '');
    }
  }

  showOrgUnitPicker(): boolean {
    return this.orgUnitOptions.length > 0;
  }

  isValid(): boolean {
    const baseValid =
      this.name().trim().length > 0 &&
      this.startYear() >= 2000 && this.startYear() <= 2100 &&
      this.durationYears() >= 1 && this.durationYears() <= 10;
    if (!this.showOrgUnitPicker()) return baseValid;
    return baseValid && this.orgUnitId().trim().length > 0;
  }

  onSave(): void {
    if (!this.isValid()) return;
    this.save.emit({
      name: this.name().trim(),
      startYear: this.startYear(),
      durationYears: this.durationYears(),
      orgUnitId: this.showOrgUnitPicker() ? this.orgUnitId() : undefined,
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.cancel.emit();
    }
  }
}
