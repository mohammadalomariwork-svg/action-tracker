import {
  Component, OnInit, OnDestroy, ChangeDetectionStrategy,
  inject, signal, computed,
} from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import {
  ReactiveFormsModule, FormBuilder, FormGroup,
  Validators, AbstractControl,
} from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { ActionItemService } from '../../../core/services/action-item.service';
import { UserService }       from '../../../core/services/user.service';
import { ToastService }      from '../../../core/services/toast.service';

import {
  ActionItem, ActionItemCreate,
  ActionStatus, ActionPriority, ActionCategory,
} from '../../../core/models/action-item.model';
import { UserProfile }       from '../../../core/models/user.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

// ── Option lists ──────────────────────────────────────────────────────────────

export const CATEGORY_OPTIONS: { value: ActionCategory; label: string }[] = [
  { value: ActionCategory.Operations,    label: 'Operations'    },
  { value: ActionCategory.Strategic,     label: 'Strategic'     },
  { value: ActionCategory.HR,            label: 'HR'            },
  { value: ActionCategory.Finance,       label: 'Finance'       },
  { value: ActionCategory.IT,            label: 'IT'            },
  { value: ActionCategory.Compliance,    label: 'Compliance'    },
  { value: ActionCategory.Communication, label: 'Communication' },
];

export const PRIORITY_OPTIONS: { value: ActionPriority; label: string; color: string; dot: string }[] = [
  { value: ActionPriority.Critical, label: 'Critical', color: '#dc2626', dot: '🔴' },
  { value: ActionPriority.High,     label: 'High',     color: '#ea580c', dot: '🟠' },
  { value: ActionPriority.Medium,   label: 'Medium',   color: '#d97706', dot: '🟡' },
  { value: ActionPriority.Low,      label: 'Low',      color: '#059669', dot: '🟢' },
];

export const STATUS_OPTIONS: { value: ActionStatus; label: string }[] = [
  { value: ActionStatus.ToDo,       label: 'To Do'       },
  { value: ActionStatus.InProgress, label: 'In Progress' },
  { value: ActionStatus.InReview,   label: 'In Review'   },
  { value: ActionStatus.Done,       label: 'Done'        },
  { value: ActionStatus.Overdue,    label: 'Overdue'     },
];

const DESC_MAX = 5000;
const NOTES_MAX = 2000;
const TODAY = new Date().toISOString().slice(0, 10);

@Component({
  selector: 'app-action-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, PageHeaderComponent],
  templateUrl: './action-form.component.html',
  styleUrl:    './action-form.component.scss',
})
export class ActionFormComponent implements OnInit, OnDestroy {
  private readonly fb         = inject(FormBuilder);
  private readonly route      = inject(ActivatedRoute);
  private readonly router     = inject(Router);
  private readonly actionSvc  = inject(ActionItemService);
  private readonly userSvc    = inject(UserService);
  private readonly toastSvc   = inject(ToastService);
  private readonly destroy$   = new Subject<void>();

  // ── State ─────────────────────────────────────────────
  readonly isEditMode   = signal(false);
  readonly editItem     = signal<ActionItem | null>(null);
  readonly teamMembers  = signal<UserProfile[]>([]);
  readonly saving       = signal(false);
  readonly loadingItem  = signal(false);

  readonly pageTitle = computed(() => {
    const item = this.editItem();
    return item
      ? `Edit: ${item.actionId} – ${item.title}`
      : 'New Action Item';
  });

  readonly pageIcon = computed(() => this.isEditMode() ? '✏️' : '➕');

  readonly descLength  = computed(() => (this.form?.get('description')?.value ?? '').length);
  readonly notesLength = computed(() => (this.form?.get('notes')?.value ?? '').length);
  readonly progressVal = computed(() => +(this.form?.get('progress')?.value ?? 0));

  // ── Constants exposed to template ─────────────────────
  readonly CATEGORY_OPTIONS = CATEGORY_OPTIONS;
  readonly PRIORITY_OPTIONS = PRIORITY_OPTIONS;
  readonly STATUS_OPTIONS   = STATUS_OPTIONS;
  readonly ActionStatus     = ActionStatus;
  readonly DESC_MAX  = DESC_MAX;
  readonly NOTES_MAX = NOTES_MAX;
  readonly TODAY     = TODAY;

  // ── Form ──────────────────────────────────────────────
  readonly form: FormGroup = this.fb.group({
    title:       ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(DESC_MAX)]],
    assigneeId:  [''],
    category:    ['', Validators.required],
    priority:    ['', Validators.required],
    status:      [ActionStatus.ToDo, Validators.required],
    dueDate:     ['', Validators.required],
    progress:    [0,  [Validators.min(0), Validators.max(100)]],
    isEscalated: [false],
    notes:       ['', [Validators.maxLength(NOTES_MAX)]],
  });

  // ── Lifecycle ─────────────────────────────────────────
  ngOnInit(): void {
    this.userSvc.getAll().subscribe({
      next: r => this.teamMembers.set((r.data ?? []).filter(u => u.isActive)),
      error: () => {},
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode.set(true);
      this.loadForEdit(id);
    }

    this.wireValueChanges();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Load for edit ─────────────────────────────────────
  private loadForEdit(id: string): void {
    this.loadingItem.set(true);
    this.actionSvc.getById(id).subscribe({
      next: r => {
        const item = r.data;
        this.editItem.set(item);
        this.form.patchValue({
          title:       item.title,
          description: item.description,
          assigneeId:  item.assigneeId,
          category:    item.category,
          priority:    item.priority,
          status:      item.status,
          dueDate:     item.dueDate.slice(0, 10),
          progress:    item.progress,
          isEscalated: item.isEscalated,
          notes:       item.notes,
        });
        this.loadingItem.set(false);
      },
      error: () => {
        this.loadingItem.set(false);
        this.toastSvc.error('Failed to load action item.');
        this.router.navigate(['/actions']);
      },
    });
  }

  // ── Cross-field value change logic ────────────────────
  private wireValueChanges(): void {
    // Status → Done auto-sets progress to 100
    this.form.get('status')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((status: ActionStatus) => {
        if (+status === ActionStatus.Done) {
          this.form.get('progress')!.setValue(100, { emitEvent: false });
        }
      });

    // Progress → 100 offers to set Status=Done
    this.form.get('progress')!.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((val: number) => {
        if (+val === 100 && +this.form.get('status')!.value !== ActionStatus.Done) {
          if (confirm('Progress is 100%. Mark this action item as Done?')) {
            this.form.get('status')!.setValue(ActionStatus.Done, { emitEvent: false });
          }
        }
      });
  }

  // ── Accessors ─────────────────────────────────────────
  ctrl(name: string): AbstractControl { return this.form.get(name)!; }

  isInvalid(name: string): boolean {
    const c = this.ctrl(name);
    return c.invalid && c.touched;
  }

  // ── Submit ────────────────────────────────────────────
  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    const payload: ActionItemCreate = {
      title:       raw.title.trim(),
      description: raw.description?.trim() ?? '',
      assigneeId:  raw.assigneeId,
      category:    +raw.category,
      priority:    +raw.priority,
      status:      +raw.status,
      dueDate:     raw.dueDate,
      progress:    +raw.progress,
      isEscalated: !!raw.isEscalated,
      notes:       raw.notes?.trim() ?? '',
    };

    const item = this.editItem();
    const obs$ = item
      ? this.actionSvc.update(item.id, payload)
      : this.actionSvc.create(payload);

    obs$.subscribe({
      next: () => {
        this.saving.set(false);
        this.toastSvc.success(item ? 'Action item updated.' : 'Action item created.');
        this.router.navigate(['/actions']);
      },
      error: () => {
        this.saving.set(false);
        this.toastSvc.error('Failed to save action item. Please try again.');
      },
    });
  }
}
