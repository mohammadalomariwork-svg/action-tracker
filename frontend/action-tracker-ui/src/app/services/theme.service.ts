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
    const saved = localStorage.getItem(STORAGE_KEY) as Theme | null;
    this.applyTheme(saved === 'light' || saved === 'dark' ? saved : 'dark');
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
