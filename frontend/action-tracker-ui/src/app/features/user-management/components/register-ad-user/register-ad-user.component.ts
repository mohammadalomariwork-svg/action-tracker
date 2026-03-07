import {
  Component,
  OnInit,
  ChangeDetectionStrategy,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormControl,
  Validators,
} from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';

import { UserManagementService } from '../../services/user-management.service';
import {
  EmployeeSearchResult,
  RegisterUserResponse,
} from '../../models/user-management.models';

interface ADUserForm {
  email:       FormControl<string>;
  fullName:    FormControl<string>;
  department:  FormControl<string>;
  jobTitle:    FormControl<string>;
  phoneNumber: FormControl<string>;
  roleName:    FormControl<string>;
}

const SEARCH_PAGE_SIZE = 10;

@Component({
  selector: 'app-register-ad-user',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './register-ad-user.component.html',
  styleUrl: './register-ad-user.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterADUserComponent implements OnInit {
  private readonly userMgmtService = inject(UserManagementService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  // ── Search state ─────────────────────────────────────────────────────────────
  readonly searchName        = signal('');
  readonly searchArabicName  = signal('');
  readonly searchEmpNo       = signal('');
  readonly searchEmail       = signal('');
  readonly loading           = signal(false);
  readonly searchResults     = signal<EmployeeSearchResult[]>([]);
  readonly searchError       = signal<string | null>(null);
  readonly searchPage        = signal(1);
  readonly hasMore           = signal(false);

  private readonly searchTrigger = new Subject<void>();

  // ── Register form state ───────────────────────────────────────────────────────
  readonly selectedEmployee = signal<EmployeeSearchResult | null>(null);
  readonly submitting = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly formError = signal<string | null>(null);

  readonly availableRoles = ['Admin', 'Manager', 'User', 'Viewer'] as const;

  readonly form: FormGroup<ADUserForm> = this.fb.group({
    email:       this.fb.nonNullable.control({ value: '', disabled: true }),
    fullName:    this.fb.nonNullable.control({ value: '', disabled: true }),
    department:  this.fb.nonNullable.control({ value: '', disabled: true }),
    jobTitle:    this.fb.nonNullable.control({ value: '', disabled: true }),
    phoneNumber: this.fb.nonNullable.control(''),
    roleName:    this.fb.nonNullable.control('', [Validators.required]),
  }) as FormGroup<ADUserForm>;

  ngOnInit(): void {
    this.searchTrigger
      .pipe(debounceTime(400), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const term = this.activeSearchTerm();
        if (term) {
          this.runSearch(term, 1);
        } else {
          this.searchResults.set([]);
          this.hasMore.set(false);
        }
      });
  }

  // ── Search ────────────────────────────────────────────────────────────────────

  onNameInput(event: Event): void {
    this.searchName.set((event.target as HTMLInputElement).value);
    this.searchTrigger.next();
  }

  onArabicNameInput(event: Event): void {
    this.searchArabicName.set((event.target as HTMLInputElement).value);
    this.searchTrigger.next();
  }

  onEmpNoInput(event: Event): void {
    this.searchEmpNo.set((event.target as HTMLInputElement).value);
    this.searchTrigger.next();
  }

  onEmailInput(event: Event): void {
    this.searchEmail.set((event.target as HTMLInputElement).value);
    this.searchTrigger.next();
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.triggerSearch();
    }
  }

  /** Returns the first non-empty field value to use as the backend searchTerm. */
  private activeSearchTerm(): string {
    return (
      this.searchName().trim() ||
      this.searchArabicName().trim() ||
      this.searchEmpNo().trim() ||
      this.searchEmail().trim()
    );
  }

  triggerSearch(): void {
    const term = this.activeSearchTerm();
    if (term) {
      this.runSearch(term, 1);
    }
  }

  loadMore(): void {
    const term = this.activeSearchTerm();
    if (term) {
      this.runSearch(term, this.searchPage() + 1, true);
    }
  }

  private runSearch(searchTerm: string, page: number, append = false): void {
    this.loading.set(true);
    this.searchError.set(null);

    this.userMgmtService
      .searchEmployees(searchTerm, page, SEARCH_PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (results) => {
          this.searchPage.set(page);
          this.hasMore.set(results.length === SEARCH_PAGE_SIZE);
          this.searchResults.set(
            append ? [...this.searchResults(), ...results] : results
          );
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.searchError.set(err.error?.message ?? 'Search failed. Please try again.');
          this.loading.set(false);
        },
      });
  }

  // ── Select employee → show form ───────────────────────────────────────────────

  selectEmployee(emp: EmployeeSearchResult): void {
    this.selectedEmployee.set(emp);
    this.formError.set(null);
    this.successMessage.set(null);

    this.form.patchValue({
      email:       emp.email,
      fullName:    emp.fullName,
      department:  emp.department  ?? '',
      jobTitle:    emp.jobTitle    ?? '',
      phoneNumber: emp.phoneNumber ?? '',
      roleName:    '',
    });
  }

  backToSearch(): void {
    this.selectedEmployee.set(null);
    this.formError.set(null);
    this.successMessage.set(null);
    this.form.get('roleName')!.reset('');
  }

  // ── Submit ────────────────────────────────────────────────────────────────────

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const emp = this.selectedEmployee()!;
    const { phoneNumber, roleName } = this.form.getRawValue();

    this.submitting.set(true);
    this.formError.set(null);

    this.userMgmtService
      .registerADUser({
        email:       emp.email,
        fullName:    emp.fullName,
        employeeId:  emp.employeeId,
        department:  emp.department,
        jobTitle:    emp.jobTitle,
        roleName,
        ...(phoneNumber ? { phoneNumber } : {}),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: RegisterUserResponse) => {
          this.submitting.set(false);
          this.successMessage.set(`"${res.fullName}" has been registered successfully.`);
          setTimeout(() => this.router.navigate(['/admin/users']), 2000);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          if (err.status === 409) {
            this.formError.set('This email is already registered.');
          } else {
            this.formError.set(
              err.error?.message ?? 'An unexpected error occurred. Please try again.'
            );
          }
        },
      });
  }

  cancel(): void {
    this.router.navigate(['/admin/users']);
  }
}
