import { Component, OnInit, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';

import { BudgetService } from '../../../services/budget.service';
import { ProjectBudget, Contract } from '../../../models/project.models';

@Component({
  selector: 'app-budget-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './budget-panel.component.html',
  styleUrl: './budget-panel.component.scss',
})
export class BudgetPanelComponent implements OnInit {
  @Input({ required: true }) projectId!: number;
  @Input() canEdit = false;

  readonly Math = Math;

  private readonly budgetService = inject(BudgetService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  budget: ProjectBudget | null = null;
  contracts: Contract[] = [];
  isLoading = false;
  errorMessage: string | null = null;

  showBudgetForm = false;
  showContractForm = false;
  editingContractId: number | null = null;

  budgetForm!: FormGroup;
  contractForm!: FormGroup;

  ngOnInit(): void {
    this.buildForms();
    this.loadData();
  }

  // ── Budget helpers ──────────────────────────────────────────────────────────

  get spentPercentage(): number {
    if (!this.budget || this.budget.totalBudget === 0) return 0;
    return Math.round((this.budget.spentAmount / this.budget.totalBudget) * 100);
  }

  get budgetProgressClass(): string {
    const pct = this.spentPercentage;
    if (pct >= 100) return 'bp-progress-red';
    if (pct >= 80) return 'bp-progress-orange';
    return 'bp-progress-green';
  }

  get isOverBudget(): boolean {
    return !!this.budget && this.budget.spentAmount > this.budget.totalBudget;
  }

  // ── Budget actions ──────────────────────────────────────────────────────────

  onEditBudget(): void {
    this.showBudgetForm = true;
    if (this.budget) {
      this.budgetForm.patchValue({
        totalBudget: this.budget.totalBudget,
        spentAmount: this.budget.spentAmount,
        currency: this.budget.currency,
        budgetNotes: this.budget.budgetNotes ?? '',
      });
    }
  }

  onAddBudget(): void {
    this.showBudgetForm = true;
    this.budgetForm.reset({
      totalBudget: '',
      spentAmount: 0,
      currency: 'AED',
      budgetNotes: '',
    });
  }

  onCancelBudgetForm(): void {
    this.showBudgetForm = false;
  }

  onSaveBudget(): void {
    if (this.budgetForm.invalid) {
      this.budgetForm.markAllAsTouched();
      return;
    }

    const v = this.budgetForm.value;
    this.budgetService
      .createOrUpdate({
        id: this.budget?.id,
        projectId: this.projectId,
        totalBudget: v.totalBudget,
        spentAmount: v.spentAmount,
        currency: v.currency,
        budgetNotes: v.budgetNotes || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.showBudgetForm = false;
          this.loadData();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to save budget.';
        },
      });
  }

  // ── Contract actions ────────────────────────────────────────────────────────

  onAddContract(): void {
    this.editingContractId = null;
    this.showContractForm = true;
    this.contractForm.reset({
      contractNumber: '',
      contractorName: '',
      contractorContact: '',
      contractValue: '',
      currency: 'AED',
      startDate: '',
      endDate: '',
      description: '',
    });
  }

  onEditContract(contract: Contract): void {
    this.editingContractId = contract.id;
    this.showContractForm = true;
    this.contractForm.patchValue({
      contractNumber: contract.contractNumber,
      contractorName: contract.contractorName,
      contractorContact: contract.contractorContact ?? '',
      contractValue: contract.contractValue,
      currency: contract.currency,
      startDate: contract.startDate
        ? new Date(contract.startDate).toISOString().substring(0, 10)
        : '',
      endDate: contract.endDate
        ? new Date(contract.endDate).toISOString().substring(0, 10)
        : '',
      description: contract.description ?? '',
    });
  }

  onCancelContractForm(): void {
    this.showContractForm = false;
    this.editingContractId = null;
  }

  onSaveContract(): void {
    if (this.contractForm.invalid) {
      this.contractForm.markAllAsTouched();
      return;
    }

    const v = this.contractForm.value;
    const payload: Partial<Contract> = {
      projectId: this.projectId,
      contractNumber: v.contractNumber,
      contractorName: v.contractorName,
      contractorContact: v.contractorContact || undefined,
      contractValue: v.contractValue,
      currency: v.currency,
      startDate: v.startDate,
      endDate: v.endDate || undefined,
      description: v.description || undefined,
      isActive: true,
    };

    const save$ = this.editingContractId
      ? this.budgetService.updateContract(this.editingContractId, { ...payload, id: this.editingContractId })
      : this.budgetService.createContract(payload);

    save$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showContractForm = false;
        this.editingContractId = null;
        this.loadData();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Failed to save contract.';
      },
    });
  }

  onDeleteContract(id: number): void {
    if (!confirm('Are you sure you want to delete this contract?')) return;

    this.budgetService
      .deleteContract(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadData(),
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to delete contract.';
        },
      });
  }

  // ── Form helpers ────────────────────────────────────────────────────────────

  hasBudgetError(field: string, error: string): boolean {
    const ctrl = this.budgetForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isBudgetInvalid(field: string): boolean {
    const ctrl = this.budgetForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  hasContractError(field: string, error: string): boolean {
    const ctrl = this.contractForm.get(field);
    return !!(ctrl?.touched && ctrl.hasError(error));
  }

  isContractInvalid(field: string): boolean {
    const ctrl = this.contractForm.get(field);
    return !!(ctrl?.touched && ctrl.invalid);
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-AE', {
      style: 'currency',
      currency: currency || 'AED',
      minimumFractionDigits: 2,
    }).format(amount);
  }

  // ── Private ─────────────────────────────────────────────────────────────────

  private buildForms(): void {
    this.budgetForm = this.fb.group({
      totalBudget: [null, [Validators.required, Validators.min(0.01)]],
      spentAmount: [0, [Validators.required, Validators.min(0)]],
      currency: ['AED', [Validators.required]],
      budgetNotes: [''],
    });

    this.contractForm = this.fb.group({
      contractNumber: ['', [Validators.required]],
      contractorName: ['', [Validators.required]],
      contractorContact: [''],
      contractValue: [null, [Validators.required, Validators.min(0)]],
      currency: ['AED', [Validators.required]],
      startDate: ['', [Validators.required]],
      endDate: [''],
      description: [''],
    });
  }

  private loadData(): void {
    this.isLoading = true;
    this.errorMessage = null;

    forkJoin({
      budget: this.budgetService.getByProject(this.projectId),
      contracts: this.budgetService.getContracts(this.projectId),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          const b = (result.budget as any)?.data ?? result.budget;
          this.budget = b ?? null;
          const c = (result.contracts as any)?.data ?? result.contracts;
          this.contracts = Array.isArray(c) ? c : [];
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load budget data.';
          this.isLoading = false;
        },
      });
  }
}
