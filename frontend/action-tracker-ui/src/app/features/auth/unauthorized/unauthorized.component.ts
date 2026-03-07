import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  template: `
    <div class="unauth-page">
      <div class="unauth-card">

        <div class="unauth-icon" aria-hidden="true">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"
               fill="none" stroke="currentColor" stroke-width="1.5"
               stroke-linecap="round" stroke-linejoin="round">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            <line x1="9" y1="9" x2="15" y2="15"/>
            <line x1="15" y1="9" x2="9" y2="15"/>
          </svg>
        </div>

        <h1 class="unauth-code">403</h1>
        <h2 class="unauth-title">Access Denied</h2>
        <p class="unauth-message">You are not authorized to view this page.</p>

        <button class="unauth-btn" type="button" (click)="goToDashboard()">
          Back to Dashboard
        </button>

      </div>
    </div>
  `,
  styles: [`
    .unauth-page {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--bg-main);
      padding: 2rem;
    }

    .unauth-card {
      background: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--border-radius);
      box-shadow: var(--shadow-md);
      padding: 3rem 2.5rem;
      text-align: center;
      max-width: 420px;
      width: 100%;
    }

    .unauth-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 72px;
      height: 72px;
      margin: 0 auto 1.25rem;
      border-radius: 50%;
      background: rgba(220, 38, 38, 0.08);
      color: #dc2626;

      svg {
        width: 36px;
        height: 36px;
      }
    }

    .unauth-code {
      margin: 0 0 0.25rem;
      font-size: 3.5rem;
      font-weight: 800;
      line-height: 1;
      background: linear-gradient(135deg, #4f46e5, #7c3aed);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .unauth-title {
      margin: 0 0 0.75rem;
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-primary);
    }

    .unauth-message {
      margin: 0 0 2rem;
      font-size: 0.9rem;
      color: var(--text-secondary);
      line-height: 1.6;
    }

    .unauth-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.65rem 1.5rem;
      background: var(--primary);
      color: #fff;
      border: none;
      border-radius: 10px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s, transform 0.15s;

      &:hover {
        background: var(--primary-dark);
        transform: translateY(-1px);
      }
    }
  `],
})
export class UnauthorizedComponent {
  private readonly router = inject(Router);

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }
}
