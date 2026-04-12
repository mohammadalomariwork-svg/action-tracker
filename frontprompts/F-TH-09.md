# F-TH-09 — Dashboard Pages: TeamDashboard, ManagementDashboard

## Context
This prompt reskins both dashboard pages. The stat cards, chart areas, and list sections all get the dark neon treatment.

## Requirements

### 1. `TeamDashboardComponent` SCSS

**Welcome banner:** No card — directly on content. Title `clamp(18px,4vw,24px)`, `var(--t1)`. Date subtitle `12.5px`, `var(--t3)`. Margin-bottom 24px.

**KPI stat cards grid:**
- `.stats-grid`: CSS Grid, `gap: 12px`
- Responsive: 2 cols (≤480px) → 3 cols (600px) → 4 cols (900px) → 6 cols (1200px)
- Each card: KpiCard styles from F-TH-07, `.card-glow` hover

**Workspace summary section:** Card treatment per workspace card, grid layout (2 cols desktop, 1 col mobile)

**My Actions / Recent Actions lists:** Card wrapper, compact rows with status badge + title + due date. Overdue items get red left border.

**Status breakdown chart:**
- Card wrapper
- Chart.js canvas: transparent background
- Update Chart.js color scheme: grid lines `rgba(var(--ar), 0.06)`, tick labels `var(--t3)`, legend labels `var(--t2)`
- Tooltip: `var(--el)` bg, `var(--bd)` border, `var(--t1)` text

### 2. `ManagementDashboardComponent` SCSS

**Same patterns as team dashboard plus:**
- Team workload bars: accent gradient fills
- At-risk items: card with red/amber left borders per severity
- Critical actions list: compact card rows

### 3. Chart.js Global Theme Override

Create `src/assets/styles/_charts.scss` with global Chart.js defaults. Or add a chart theming configuration in styles that targets canvas containers:
- Canvas wrapper backgrounds: transparent
- Grid colors: `rgba(var(--ar), 0.06)`
- Text colors: `var(--t3)` for ticks, `var(--t2)` for labels
- Tooltip background: `var(--el)` with `backdrop-filter: blur(12px)`

Import in `styles.scss`.

## Rules
- Modify SCSS only for both dashboard components
- Create `_charts.scss` global partial
- Do NOT change chart data, datasets, labels, or any TypeScript
- Chart colors should be updated via Chart.js options objects if needed (minimal TS to set color configs — NOT data changes)
