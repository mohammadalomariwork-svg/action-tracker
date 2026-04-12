# F-TH-04 — Header — Full Width, Logo, Theme Toggle, Hamburger Menu, Off-Canvas Drawer

## Context
This prompt reskins the `HeaderComponent`. The header must be full-width (no side margins), sitting ABOVE the content wrapper.

## Requirements

### 1. Update `HeaderComponent` SCSS

Apply dark neon header styling:
- `position: sticky; top: 0; z-index: 100; height: 60px; width: 100%`
- `background: var(--nav); backdrop-filter: blur(24px); border-bottom: 1px solid var(--bd)`
- `display: flex; align-items: center; justify-content: space-between; padding: 0 24px; gap: 12px`
- No side borders — header is independent of the content margins
- Mobile: `height: 56px; padding: 0 16px`

**Logo area:**
- Replace the existing logo/text with the Actions Center SVG logo mark (`logo-mark` class, 34px, with neon `drop-shadow`)
- Text: "Actions" in `var(--t1)` + "Center" in `var(--accent)` + "KHALIFA UNIVERSITY" subtitle (8.5px, uppercase, `var(--t3)`, hidden on mobile)

**Nav links (desktop only, hidden <768px):**
- Horizontal list, 12.5px, font-weight 600, `var(--t3)` color
- Hover: `var(--t1)` color, `var(--bg-surface)` background
- Active: `var(--accent)` color, `rgba(var(--ar), 0.07)` background
- Each link has a Bootstrap Icon before the text

**Right side:**
- Theme toggle button: `bi-sun` (dark mode) / `bi-moon-stars` (light mode), 36px square, card-style bg/border
  - Inject `ThemeService`, call `toggleTheme()` on click
  - Subscribe to `isDark$` to toggle icon
- Notification bell (existing — just restyle the button to match icon-button pattern)
- User avatar (existing — restyle to gradient bg with accent colors)

**Hamburger button (mobile <768px only):**
- `bi-list` icon, 36px square, same icon-button style
- `display: none` on desktop, `display: flex` on mobile
- On click: opens the off-canvas drawer

### 2. Add off-canvas mobile navigation drawer

Add to the `HeaderComponent` template (or a child component):
- Overlay: fixed inset, `rgba(0,0,0,0.50)`, `backdrop-filter: blur(4px)`, z-index 200. Click to close.
- Drawer: fixed right, 280px width (max 80vw), `var(--bg)` background, `border-left: 1px solid var(--bd)`, z-index 201
  - Slide-in from right: `transform: translateX(100%)` → `translateX(0)` on open
  - Header with logo + close button (`bi-x-lg`)
  - Vertical nav links with icons, 14px, padded, same active style as desktop
  - Same nav links as desktop — duplicated for mobile (same `routerLink` bindings)
- Toggle with a boolean `isNavOpen` in the component

### 3. Ensure the header renders OUTSIDE any content margin wrapper

If the `LayoutComponent` currently wraps everything (header + router-outlet + footer) inside a single container, restructure so:
- Header is rendered at top level with no margin wrapper
- Content area (router-outlet) is wrapped with `margin: 0 var(--M)` and side borders
- Footer is rendered at top level with no margin wrapper

This may require a small HTML restructure in `LayoutComponent` template.

## Rules
- Modify HeaderComponent SCSS and HTML template
- Modify LayoutComponent HTML template (restructure nesting only — no logic changes)
- Add ThemeService injection to HeaderComponent (minimal TypeScript — only for toggle button)
- Add `isNavOpen` boolean + `openNav()`/`closeNav()` methods (minimal TypeScript)
- Do NOT change any existing navigation logic, permission checks, or dynamic menu rendering
- Keep all existing `*ngIf` permission-based menu visibility intact
