import { Component, input, computed } from '@angular/core';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  templateUrl: './progress-bar.component.html',
  styleUrl: './progress-bar.component.scss',
})
export class ProgressBarComponent {
  readonly value = input.required<number>();

  readonly clamped = computed(() => Math.min(100, Math.max(0, this.value())));

  readonly colorClass = computed(() => {
    const v = this.clamped();
    if (v >= 75) return 'progress--green';
    if (v >= 40) return 'progress--blue';
    return 'progress--red';
  });
}
