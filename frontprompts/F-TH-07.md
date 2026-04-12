# F-TH-07 — Shared Display Components: Badges, ProgressBar, KpiCard, Breadcrumb, PageHeader

## Context
This prompt reskins all shared display components used across multiple pages. Only SCSS changes — no content changes.

## Requirements

### 1. `StatusBadgeComponent` SCSS
- Pill shape: `border-radius: var(--r-full); padding: 3px 10px`
- `font-size: 10px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase`
- `::before` pseudo dot: 5px circle, `background: currentColor`
- Color mapping using themed rgba backgrounds:
  - ToDo: gray `rgba(148,163,184,0.08)`, color `#94a3b8`
  - InProgress: accent `rgba(var(--ar),0.10)`, color `var(--accent)`
  - InReview: purple `rgba(var(--a2r),0.10)`, color `#a78bfa`
  - Done: green `rgba(34,197,94,0.10)`, color `#22c55e`
  - Overdue: red `rgba(239,68,68,0.10)`, color `#ef4444`

### 2. `PriorityBadgeComponent` SCSS
- Same pill pattern as StatusBadge
- Low: green, Medium: amber, High: amber, Critical: red (with neon red glow on Critical)

### 3. `ProgressBarComponent` SCSS
- Track: `height: 6px; border-radius: var(--r-full); background: rgba(var(--ar), 0.06)`
- Fill: `background: linear-gradient(90deg, var(--accent), rgba(var(--a2r), 0.8))` with `box-shadow: 0 0 8px rgba(var(--ar), 0.25)`
- 100% fill: green gradient instead of accent
- Overdue fill: red gradient with red glow

### 4. `KpiCardComponent` SCSS
- Card treatment: `var(--sf)` bg, border, backdrop-blur
- `.card-glow:hover` for neon border glow on hover
- Icon: 36px rounded square, themed accent bg at 8% opacity
- Label: `10px; font-weight: 700; var(--t3); uppercase; letter-spacing 0.07em`
- Value: `26px; font-weight: 800; letter-spacing: -0.03em`. Mobile: `22px`
- Trend: `10.5px; font-weight: 600` — green for positive, red for negative

### 5. `BreadcrumbComponent` SCSS
- No background — transparent
- Text: `font-size: 13px; color: var(--t3)`
- Separator: `bi-chevron-right`, `var(--t3)` at 50% opacity
- Current (last item): `color: var(--t1); font-weight: 600`
- Links: `var(--t3)`, hover `var(--accent)`

### 6. `PageHeaderComponent` SCSS
- No background card
- Title: `font-size: clamp(18px, 4vw, 24px); font-weight: 800; letter-spacing: -0.03em; color: var(--t1)`
- Subtitle: `font-size: 12.5px; color: var(--t3)`
- Action buttons area: flexbox row, gap 6px
- Mobile: title + buttons stack vertically

## Rules
- Modify SCSS files of each component only
- No template or TypeScript changes
- Use CSS variables everywhere
- Ensure both dark and light themes render correctly
