import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

const RATING_STYLES: Record<string, { bg: string; text: string }> = {
  Critical: { bg: '#dc3545', text: '#fff' },
  High:     { bg: '#fd7e14', text: '#fff' },
  Medium:   { bg: '#ffc107', text: '#212529' },
  Low:      { bg: '#198754', text: '#fff' },
};

@Component({
  selector: 'app-risk-rating-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span
      class="badge"
      [style.background-color]="bgColor"
      [style.color]="textColor"
    >{{ rating }}</span>
  `,
  styles: [`:host { display: inline-block; } .badge { font-size: 0.78rem; }`],
})
export class RiskRatingBadgeComponent {
  @Input({ required: true }) rating = '';

  get bgColor(): string {
    return RATING_STYLES[this.rating]?.bg ?? '#6c757d';
  }

  get textColor(): string {
    return RATING_STYLES[this.rating]?.text ?? '#fff';
  }
}
