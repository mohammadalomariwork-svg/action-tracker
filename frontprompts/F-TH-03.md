# F-TH-03 — Theme Toggle Service + localStorage Persistence

## Context
F-TH-01 defined CSS variables for dark/light. This prompt creates a service to toggle the theme and persist the user's choice.

## Requirements

### 1. Create `ThemeService` in `src/app/services/theme.service.ts`

```
providedIn: 'root'

Properties:
  currentTheme$: BehaviorSubject<'dark' | 'light'>
  isDark$: Observable<boolean>

Methods:
  initTheme(): void
    — Read 'ku-theme' from localStorage
    — If found, apply it; otherwise default to 'dark'
    — Set data-theme attribute on document.documentElement

  toggleTheme(): void
    — Switch between 'dark' and 'light'
    — Update data-theme attribute on document.documentElement
    — Save to localStorage as 'ku-theme'
    — Emit new value on currentTheme$

  setTheme(theme: 'dark' | 'light'): void
    — Explicit set
```

### 2. Call `themeService.initTheme()` in `APP_INITIALIZER`

Add to the existing APP_INITIALIZER factory (where permissions are loaded). This ensures the theme is applied before the first render.

### 3. The HeaderComponent will consume this service for the toggle button (handled in F-TH-04)

This prompt only creates the service and initializes it. The UI toggle is wired in the next prompt.

## Rules
- One new service file
- One modification to APP_INITIALIZER
- No visual changes in this prompt
- localStorage key: `ku-theme`
