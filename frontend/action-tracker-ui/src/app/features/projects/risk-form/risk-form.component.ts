import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  output,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';

import { ProjectRiskService } from '../../../services/project-risk.service';
import { ActionItemService } from '../../../core/services/action-item.service';
import { ToastService } from '../../../core/services/toast.service';
import { ProjectRisk, RISK_CATEGORIES, RISK_STATUSES } from '../../../models/project-risk.model';
import { AssignableUser } from '../../../core/models/action-item.model';
import { RiskRatingBadgeComponent } from '../../../shared/components/risk-rating-badge/risk-rating-badge.component';

declare const bootstrap: {
  Modal: new (el: HTMLElement, opts?: object) => { show(): void; hide(): void; dispose(): void };
};

const PROB_LABELS: Record<number, string> = {
  1: 'Rare', 2: 'Unlikely', 3: 'Possible', 4: 'Likely', 5: 'Almost Certain',
};
const IMPACT_LABELS: Record<number, string> = {
  1: 'Negligible', 2: 'Minor', 3: 'Moderate', 4: 'Major', 5: 'Severe',
};

interface RiskForm {
  title:           FormControl<string>;
  description:     FormControl<string>;
  category:        FormControl<string>;
  probabilityScore: FormControl<number>;
  impactScore:     FormControl<number>;
  status:          FormControl<string>;
  riskOwnerUserId: FormControl<string>;
  dueDate:         FormControl<string>;
  mitigationPlan:  FormControl<string>;
  contingencyPlan: FormControl<string>;
  notes:           FormControl<string>;
}

@Component({
  selector: 'app-risk-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RiskRatingBadgeComponent],
  templateUrl: './risk-form.component.html',
  styleUrl: './risk-form.component.scss',
})
export class RiskFormComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input({ required: true }) projectId!: string;
  @Input() risk: ProjectRisk | null = null;
  @ViewChild('modalEl') modalEl!: ElementRef<HTMLElement>;

  saved = output<ProjectRisk>();
  cancelled = output<void>();

  private readonly fb          = inject(FormBuilder);
  private readonly riskService = inject(ProjectRiskService);
  private readonly userService = inject(ActionItemService);
  private readonly toast       = inject(ToastService);

  private bsModal!: { show(): void; hide(): void; dispose(): void };

  readonly categories  = RISK_CATEGORIES;
  readonly statuses    = RISK_STATUSES;
  readonly probLabels  = PROB_LABELS;
  readonly impactLabels = IMPACT_LABELS;
  readonly scores      = [1, 2, 3, 4, 5];

  saving = signal(false);
  users  = signal<AssignableUser[]>([]);

  form: FormGroup<RiskForm> = this.fb.group({
    title:           this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(300)]),
    description:     this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(2000)]),
    category:        this.fb.nonNullable.control('', Validators.required),
    probabilityScore: this.fb.nonNullable.control(3, [Validators.required, Validators.min(1), Validators.max(5)]),
    impactScore:     this.fb.nonNullable.control(3, [Validators.required, Validators.min(1), Validators.max(5)]),
    status:          this.fb.nonNullable.control('Open', Validators.required),
    riskOwnerUserId: this.fb.nonNullable.control(''),
    dueDate:         this.fb.nonNullable.control(''),
    mitigationPlan:  this.fb.nonNullable.control('', Validators.maxLength(2000)),
    contingencyPlan: this.fb.nonNullable.control('', Validators.maxLength(2000)),
    notes:           this.fb.nonNullable.control('', Validators.maxLength(2000)),
  });

  get isEdit(): boolean {
    return this.risk !== null;
  }

  get computedScore(): number {
    return this.form.controls.probabilityScore.value * this.form.controls.impactScore.value;
  }

  get computedRating(): string {
    const s = this.computedScore;
    if (s >= 20) return 'Critical';
    if (s >= 12) return 'High';
    if (s >= 5) return 'Medium';
    return 'Low';
  }

  ngOnInit(): void {
    this.userService.getAssignableUsers().subscribe({
      next: res => { if (res.success) this.users.set(res.data); },
    });

    if (this.risk) {
      this.form.patchValue({
        title:           this.risk.title,
        description:     this.risk.description,
        category:        this.risk.category,
        probabilityScore: this.risk.probabilityScore,
        impactScore:     this.risk.impactScore,
        status:          this.risk.status,
        riskOwnerUserId: this.risk.riskOwnerUserId ?? '',
        dueDate:         this.risk.dueDate ? this.risk.dueDate.substring(0, 10) : '',
        mitigationPlan:  this.risk.mitigationPlan ?? '',
        contingencyPlan: this.risk.contingencyPlan ?? '',
        notes:           this.risk.notes ?? '',
      });
    }
  }

  ngAfterViewInit(): void {
    this.bsModal = new bootstrap.Modal(this.modalEl.nativeElement, { backdrop: 'static' });
    this.modalEl.nativeElement.addEventListener('hidden.bs.modal', () => {
      this.cancelled.emit();
    });
    this.bsModal.show();
  }

  ngOnDestroy(): void {
    this.bsModal?.dispose();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.getRawValue();

    if (this.isEdit) {
      this.riskService.updateRisk(this.projectId, this.risk!.id, {
        title:           v.title,
        description:     v.description,
        category:        v.category,
        probabilityScore: v.probabilityScore,
        impactScore:     v.impactScore,
        status:          v.status,
        riskOwnerUserId: v.riskOwnerUserId || undefined,
        dueDate:         v.dueDate || undefined,
        mitigationPlan:  v.mitigationPlan || undefined,
        contingencyPlan: v.contingencyPlan || undefined,
        notes:           v.notes || undefined,
      }).subscribe({
        next: res => {
          this.saving.set(false);
          if (res.success) {
            this.toast.success('Risk updated successfully.');
            this.bsModal.hide();
            this.saved.emit(res.data);
          }
        },
        error: () => { this.saving.set(false); this.toast.error('Failed to update risk.'); },
      });
    } else {
      this.riskService.createRisk(this.projectId, {
        projectId:       this.projectId,
        title:           v.title,
        description:     v.description,
        category:        v.category,
        probabilityScore: v.probabilityScore,
        impactScore:     v.impactScore,
        status:          v.status !== 'Open' ? v.status : undefined,
        riskOwnerUserId: v.riskOwnerUserId || undefined,
        dueDate:         v.dueDate || undefined,
        mitigationPlan:  v.mitigationPlan || undefined,
        contingencyPlan: v.contingencyPlan || undefined,
        notes:           v.notes || undefined,
      }).subscribe({
        next: res => {
          this.saving.set(false);
          if (res.success) {
            this.toast.success('Risk created successfully.');
            this.bsModal.hide();
            this.saved.emit(res.data);
          }
        },
        error: () => { this.saving.set(false); this.toast.error('Failed to create risk.'); },
      });
    }
  }

  close(): void {
    this.bsModal.hide();
  }
}
