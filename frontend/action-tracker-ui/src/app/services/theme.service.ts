import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

type Theme = 'dark' | 'light';
const STORAGE_KEY = 'ku-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly currentTheme$ = new BehaviorSubject<Theme>('dark');
  readonly isDark$: Observable<boolean> = this.currentTheme$.pipe(map(t => t === 'dark'));

  initTheme(): void {
    // Force dark mode until light mode is enhanced
    this.applyTheme('dark');
  }

  toggleTheme(): void {
    this.applyTheme(this.currentTheme$.value === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: Theme): void {
    this.applyTheme(theme);
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(STORAGE_KEY, theme);
    this.currentTheme$.next(theme);
  }
}
