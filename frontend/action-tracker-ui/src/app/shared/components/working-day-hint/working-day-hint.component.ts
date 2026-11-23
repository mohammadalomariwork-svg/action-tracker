import {
  ChangeDetectionStrategy, Component, ElementRef, Injector,
  afterNextRender, computed, inject, input, output,
} from '@angular/core';
import type { WorkspaceCalendarRules } from '../../../services/workspace-calendar.service';
import { checkWorkingDay } from '../../validators/working-day.validator';

/**
 * Inline warning under a date field when the chosen date is not a working day
 * in the workspace's calendar (WO-2026-11 C-3), with a one-click switch to the
 * next working day. Shows nothing for an unchanged date (`original`), while the
 * calendar is loading, or for a date outside the loaded window.
 *
 * ```html
 * <input type="date" formControlName="dueDate" />
 * <app-working-day-hint [calendar]="calendar()" [date]="form.value.dueDate"
 *                       [original]="originalDueDate" (useDate)="form.patchValue({ dueDate: $event })" />
 * ```
 * Place it in the same container as its date input: taking the suggestion
 * removes the hint, so focus moves back to that input instead of the page.
 */
@Component({
  selector: 'app-working-day-hint',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (problem(); as p) {
      <div class="wd-hint" role="alert">
        <i class="bi bi-calendar-x wd-hint__icon" aria-hidden="true"></i>
        <span class="wd-hint__text">{{ p.hint }}</span>
        @if (p.nextWorkingDay && p.nextWorkingDayLabel) {
          <button type="button" class="wd-hint__fix" (click)="takeSuggestion(p.nextWorkingDay)">
            Use {{ p.nextWorkingDayLabel }}
          </button>
        }
      </div>
    }
  `,
  styles: [`
    /* Body text in --t1 on a light --err tint: the red alone falls under 4.5:1 on the dark surface. */
    .wd-hint {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0.25rem 0.5rem;
      margin-top: 0.375rem;
      padding: 0.375rem 0.5rem;
      border-left: 3px solid var(--err);
      border-radius: var(--r-sm, 0.25rem);
      background: rgba(var(--errr), 0.1);
      font-size: 0.8125rem;
      line-height: 1.35;
      color: var(--t1);
    }
    .wd-hint__icon { flex: 0 0 auto; color: var(--err); }
    .wd-hint__text { flex: 1 1 12rem; min-width: 0; }
    .wd-hint__fix {
      padding: 0;
      border: 0;
      background: none;
      font: inherit;
      font-weight: 600;
      color: var(--accent);
      text-decoration: underline;
      cursor: pointer;
      white-space: nowrap;
    }
    .wd-hint__fix:hover { text-decoration: none; }
    .wd-hint__fix:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; border-radius: 2px; }
  `],
})
export class WorkingDayHintComponent {
  /** The workspace calendar (`injectWorkspaceCalendar`); null = say nothing. */
  readonly calendar = input<WorkspaceCalendarRules | null>(null);
  /** The field's current value (`yyyy-MM-dd`). */
  readonly date = input<string | null | undefined>(null);
  /** The value the record holds now; an unchanged date is never flagged. Leave out on create. */
  readonly original = input<string | null | undefined>(null);
  /** Field label used in the server-style message. */
  readonly field = input('Date');

  /** The next working day (`yyyy-MM-dd`) when the user takes the suggestion. */
  readonly useDate = output<string>();

  readonly problem = computed(() =>
    checkWorkingDay(this.calendar(), this.date(), this.original(), this.field()));

  private readonly host     = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  takeSuggestion(date: string): void {
    // The button disappears with the hint, so hand focus back to the field.
    const field = this.host.nativeElement.parentElement?.querySelector<HTMLInputElement>('input[type="date"]');
    this.useDate.emit(date);
    afterNextRender(() => field?.focus(), { injector: this.injector });
  }
}
