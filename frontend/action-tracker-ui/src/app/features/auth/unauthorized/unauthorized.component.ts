import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  template: `
    <!-- Animated Background (duplicated — auth pages live outside the layout shell) -->
    <div class="bg">
      <div class="bg-diag"></div>
      <div class="bg-wave w1"></div>
      <div class="bg-wave w2"></div>
      <div class="bg-wave w3"></div>
      <div class="bg-orb o1"></div>
      <div class="bg-orb o2"></div>
    </div>

    <div class="unauth-page">
      <div class="unauth-card">

        <div class="unauth-icon-wrap">
          <i class="bi bi-person-x unauth-icon"></i>
        </div>

        <h1 class="unauth-title">Session Expired</h1>
        <p class="unauth-message">
          Your session has expired or you are not authorized.<br />
          Please sign in again to continue.
        </p>

        <button class="unauth-btn" type="button" (click)="goToLogin()">
          Sign In
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
      position: relative;
      z-index: 1;
      padding: 2rem;
    }

    .unauth-card {
      background: var(--el);
      border: 1px solid var(--bd);
      border-radius: var(--r-xl);
      box-shadow: var(--neon);
      backdrop-filter: blur(24px);
      padding: 3rem 2.5rem;
      text-align: center;
      max-width: 440px;
      width: 100%;
    }

    .unauth-icon-wrap {
      margin-bottom: 1.5rem;
    }

    .unauth-icon {
      font-size: 48px;
      color: var(--warning, #f59e0b);
    }

    .unauth-title {
      margin: 0 0 0.75rem;
      font-size: 20px;
      font-weight: 800;
      color: var(--t1);
    }

    .unauth-message {
      margin: 0 0 2rem;
      font-size: 13px;
      color: var(--t2);
      line-height: 1.6;
    }

    /* Neon primary button */
    .unauth-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.7rem 2rem;
      background: var(--accent);
      color: #000;
      border: none;
      border-radius: var(--r-md);
      font-size: 0.95rem;
      font-weight: 700;
      cursor: pointer;
      box-shadow: var(--neon);
      position: relative;
      overflow: hidden;
      transition: transform 0.15s, box-shadow 0.2s;

      &:hover {
        transform: translateY(-1px);
        box-shadow: var(--neon2);
      }

      &:active {
        transform: scale(0.97);
      }
    }
  `],
})
export class UnauthorizedComponent {
  private readonly router = inject(Router);

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
