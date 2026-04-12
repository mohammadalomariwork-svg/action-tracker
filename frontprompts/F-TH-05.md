# F-TH-05 — Footer Full-Width + LayoutComponent Content Wrapper

## Context
F-TH-04 restructured the header. This prompt applies the same treatment to the footer and finalizes the layout wrapper.

## Requirements

### 1. Update `FooterComponent` SCSS

- `width: 100%; position: relative; z-index: 100`
- `background: var(--nav); backdrop-filter: blur(24px)`
- `border-top: 1px solid var(--bd)`
- `padding: 18px 24px; text-align: center`
- `font-size: 11px; color: var(--t3)`
- Update copyright text to: `© 2025 Khalifa University — Actions Center`
- No side margins — footer spans full viewport width
- Footer sits OUTSIDE the content margin wrapper

### 2. Update `LayoutComponent` content wrapper

Ensure the template structure is:
```html
<!-- Background + rails already added in F-TH-02 -->
<app-header></app-header>
<div class="wrap">
  <div class="ct">
    <router-outlet></router-outlet>
  </div>
</div>
<app-footer></app-footer>
```

**`.wrap` styles:**
- `position: relative; z-index: 1`
- `margin: 0 var(--M)`
- `min-height: calc(100vh - 110px)` — fills space between header (60px) and footer (~50px)
- `border-left: 1px solid rgba(var(--ar), 0.06)`
- `border-right: 1px solid rgba(var(--ar), 0.06)`
- `padding-bottom: 24px`

**`.ct` styles:**
- `padding: 24px`
- Mobile ≤480px: `padding: 14px 12px`
- Tablet 481–768px: `padding: 18px 16px`

### 3. Update `LoadingBarComponent`

- Position fixed, top 60px (below header), full width, z-index 99
- Height 2px
- Background: `linear-gradient(90deg, transparent, var(--accent), transparent)`
- Animation: slide left→right infinite
- Glow: `box-shadow: 0 0 12px rgba(var(--ar), 0.40)`
- Mobile: top 56px

## Rules
- Modify FooterComponent SCSS + template (text update only)
- Modify LayoutComponent SCSS + template (nesting restructure only)
- Modify LoadingBarComponent SCSS only
- No TypeScript logic changes
