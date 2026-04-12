# F-TH-06 — Global Card, Button, Input, Select Styles

## Context
This prompt creates global overrides for all cards, buttons, and form controls using the theme CSS variables. These apply automatically to every component.

## Requirements

### 1. Create `src/assets/styles/_components.scss`

**Cards (`.card` class applied to existing Bootstrap cards or custom card elements):**
- `background: var(--sf); border: 1px solid var(--bd); border-radius: var(--r-lg)`
- `box-shadow: var(--csh); backdrop-filter: blur(16px)`
- Hover: `border-color: var(--bdh)`
- `.card-glow:hover`: adds `box-shadow: 0 0 24px rgba(var(--ar), 0.07)`

**Neon primary buttons (override `.btn-primary`):**
- `background: var(--accent); color: #000; border: none; border-radius: var(--r-md)`
- `font-weight: 700; box-shadow: var(--neon)`
- `::after` shimmer overlay on hover (diagonal white gradient, opacity 0→1)
- Hover: `translateY(-1px); box-shadow: var(--neon2)`
- Active: `scale(0.97)`

**Ghost buttons (override `.btn-outline-primary`, `.btn-outline-secondary`):**
- `background: var(--sf); color: var(--t2); border: 1px solid var(--bd); border-radius: var(--r-md)`
- `backdrop-filter: blur(8px)`
- Hover: brighter bg + border

**Danger buttons (override `.btn-danger`):**
- Same neon pattern but with `$danger` color and red glow shadow

**Success buttons (override `.btn-success`):**
- Same neon pattern with `$success` color and green glow

### 2. Create `src/assets/styles/_forms.scss`

**Override `.form-control`, `.form-select`:**
- `background: var(--inp); border: 1px solid var(--bd); border-radius: var(--r-md)`
- `padding: 10px 14px; color: var(--t1); font-size: 14px`
- Placeholder: `color: var(--t3)`
- Focus: `background: var(--inpf); border-color: rgba(var(--ar), 0.40); box-shadow: 0 0 0 3px rgba(var(--ar), 0.06), 0 0 16px rgba(var(--ar), 0.08)`
- Invalid: red border + red ring
- Disabled: `opacity: 0.5`

**Textareas:** same treatment, `min-height: 100px; resize: vertical`

**Labels (`.form-label`):** `font-size: 11px; font-weight: 600; color: var(--t3); letter-spacing: 0.03em`

**Checkboxes:** custom styled, `var(--accent)` fill when checked

**Date inputs:** same glass treatment, native picker on mobile

### 3. Import in `styles.scss`

Add `_components` and `_forms` imports.

## Rules
- Global style overrides — no component-specific files modified
- These override Bootstrap default styles for ALL components automatically
- Use CSS variables only — no hardcoded colors
- All existing Bootstrap class names preserved (`.btn-primary`, `.form-control`, etc.)
