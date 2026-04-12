# F-TH-18 — Final Polish: Toasts, Modals, Offcanvas, Dropdowns, Print, Accessibility

## Context
This is the final prompt. All pages and components are themed. This prompt applies global overrides for Bootstrap UI elements, toast notifications, print styles, accessibility focus indicators, and reduced-motion support.

## Requirements

### 1. Create `src/assets/styles/_overrides.scss`

Import in `styles.scss` as the LAST partial (after all others).

**ngx-toastr overrides:**
```scss
.toast-container .ngx-toastr {
  background: var(--el) !important;
  backdrop-filter: blur(20px);
  border: 1px solid var(--bd) !important;
  border-radius: var(--r-lg) !important;
  box-shadow: 0 8px 32px rgba(0,0,0,0.25) !important;
  font-family: var(--font);
  font-size: 13px;
  color: var(--t1) !important;
  padding: 14px 18px !important;

  &.toast-success {
    border-left: 3px solid #22c55e !important;
  }
  &.toast-error {
    border-left: 3px solid #ef4444 !important;
  }
  &.toast-warning {
    border-left: 3px solid #f59e0b !important;
  }
  &.toast-info {
    border-left: 3px solid var(--accent) !important;
  }
  .toast-title { font-weight: 700; }
  .toast-close-button { color: var(--t3); &:hover { color: var(--t1); } }
}
```

**Bootstrap Modal overrides:**
```scss
.modal-backdrop {
  background: rgba(0,0,0,0.50) !important;
  backdrop-filter: blur(4px);
}
.modal-content {
  background: var(--el) !important;
  border: 1px solid var(--bdh) !important;
  border-radius: var(--r-xl) !important;
  backdrop-filter: blur(24px);
  box-shadow: 0 0 40px rgba(var(--ar), 0.08) !important;
  color: var(--t1);
}
.modal-header {
  border-bottom: 1px solid var(--bd) !important;
  padding: 18px 24px;
  .modal-title { font-size: 16px; font-weight: 800; color: var(--t1); }
  .btn-close { filter: invert(1) brightness(0.7); opacity: 0.5; &:hover { opacity: 0.9; } }
}
.modal-body { padding: 24px; }
.modal-footer { border-top: 1px solid var(--bd) !important; padding: 14px 24px; }
```

Light mode: `.btn-close { filter: none; }`

**Bootstrap Offcanvas overrides:**
```scss
.offcanvas {
  background: var(--el) !important;
  border: none !important;
  backdrop-filter: blur(24px);
  color: var(--t1);
  .offcanvas-header { border-bottom: 1px solid var(--bd); padding: 18px 24px; }
  .offcanvas-body { padding: 24px; }
}
.offcanvas-backdrop {
  background: rgba(0,0,0,0.50) !important;
  backdrop-filter: blur(4px);
}
```

**Bootstrap Dropdown overrides:**
```scss
.dropdown-menu {
  background: var(--el) !important;
  border: 1px solid var(--bd) !important;
  border-radius: var(--r-lg) !important;
  backdrop-filter: blur(24px);
  box-shadow: 0 8px 32px rgba(0,0,0,0.25) !important;
  padding: 6px;

  .dropdown-item {
    border-radius: var(--r-sm);
    padding: 8px 12px;
    font-size: 13px;
    color: var(--t2);
    transition: all 0.15s;
    &:hover, &:focus { background: var(--sfh); color: var(--t1); }
    &.active, &:active { background: rgba(var(--ar), 0.08); color: var(--accent); }
  }
  .dropdown-divider { border-color: var(--bd); }
}
```

**Bootstrap Tooltip overrides:**
```scss
.tooltip .tooltip-inner {
  background: var(--el);
  color: var(--t1);
  font-size: 11px;
  font-weight: 500;
  padding: 6px 10px;
  border-radius: var(--r-sm);
  backdrop-filter: blur(12px);
  border: 1px solid var(--bd);
  box-shadow: 0 4px 16px rgba(0,0,0,0.20);
}
.tooltip .tooltip-arrow::before { display: none; }
```

### 2. Scrollbar styling (already in F-TH-01, verify it's in place)

### 3. Focus-visible indicators
```scss
*:focus-visible {
  outline: none !important;
  box-shadow: 0 0 0 2px var(--accent), 0 0 0 4px rgba(var(--ar), 0.20) !important;
  border-radius: var(--r-sm);
}
*:focus:not(:focus-visible) {
  box-shadow: none !important;
}
```

### 4. Reduced motion
```scss
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
  .bg-diag, .bg-wave, .bg-orb, .dot, .rail::after, .burst {
    animation: none !important;
  }
}
```

### 5. Print styles
```scss
@media print {
  body { background: #fff !important; color: #000 !important; }
  .bg, .rail, .burst, .crn, .nav-overlay { display: none !important; }
  .hdr { position: relative; background: #fff !important; backdrop-filter: none !important;
    border-bottom: 1px solid #ddd !important; }
  .ft { background: #fff !important; backdrop-filter: none !important;
    border-top: 1px solid #ddd !important; color: #666 !important; }
  .wrap { margin: 0 !important; border: none !important; }
  .card, [class*="card"] {
    background: #fff !important; backdrop-filter: none !important;
    box-shadow: none !important; border: 1px solid #ddd !important;
  }
  .btn { box-shadow: none !important; }
  .badge { border: 1px solid #ccc !important; }
}
```

### 6. Smooth page transitions

Add a global route transition class that can be applied to `router-outlet` content:
```scss
.route-animate {
  animation: routeIn 0.35s ease-out;
}
@keyframes routeIn {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}
```

### 7. Global link styling
```scss
a { color: var(--accent); text-decoration: none; transition: color 0.15s; }
a:hover { color: var(--accent); opacity: 0.8; }
```

### 8. Selection on dark backgrounds
Already handled in F-TH-01 — verify `::selection { background: rgba(var(--ar), 0.25); }` is present.

## Rules
- This is ALL global CSS in `_overrides.scss` — no component-specific files
- `!important` is used only where necessary to override Bootstrap defaults
- These are the LAST styles to load, so they have natural cascade priority
- No TypeScript changes
- Test: verify all Bootstrap modals, dropdowns, tooltips, and toasts render correctly in BOTH dark and light modes
- Test: verify print preview strips all glass effects and shows clean black-on-white

## Post-Implementation Verification Checklist

After all 18 prompts are complete, manually verify:
- [ ] Dark theme loads by default on first visit
- [ ] Theme toggle switches between dark and light, persists on refresh
- [ ] Animated background particles visible and moving in random directions
- [ ] Border rail beams traveling (left: down, right: up)
- [ ] Header spans full width, bottom border edge-to-edge
- [ ] Footer spans full width, top border edge-to-edge
- [ ] All stat cards have neon glow on hover
- [ ] All primary buttons have neon glow + shimmer on hover
- [ ] Data tables readable with proper contrast
- [ ] Mobile: hamburger menu opens off-canvas drawer
- [ ] Mobile: tables convert to card view at <600px
- [ ] Mobile: forms stack to single column
- [ ] Mobile: stat cards 2-column grid
- [ ] Login page: centered card on animated background
- [ ] Charts: transparent backgrounds, themed colors
- [ ] Toasts: glass treatment with colored left border
- [ ] Modals: glass backdrop + themed content
- [ ] Print: clean black-on-white, no glass effects
- [ ] prefers-reduced-motion: all animations disabled
- [ ] Focus-visible: accent ring on keyboard navigation
- [ ] WCAG AA: all text meets minimum contrast ratios
