# F-TH-13 — Admin Panel: AdminPanel, OrgChart, Objectives, KPIs

## Requirements

### 1. `AdminPanelComponent` SCSS
- If sidebar exists: card treatment, 240px width desktop, sticky below header
  - Nav items: `padding: 10px 16px; border-radius: var(--r-md); color: var(--t2)`
  - Active: `rgba(var(--ar), 0.07)` bg, `var(--accent)` color, `border-left: 3px solid var(--accent)`
  - Hover: `var(--sf)` bg
  - Icons: `var(--t3)`, active `var(--accent)`
  - Mobile: off-canvas or collapses
- Content area: flex 1, standard padding

### 2. `OrgChartListComponent` + `OrgUnitFormComponent` SCSS
- Tree list: card wrapper, indented rows `padding-left: level * 24px`
- Row hover: `var(--sfh)` bg
- Expand/collapse icons: `var(--t3)`, hover `var(--accent)`
- Action buttons: ghost icon buttons
- Form: modal or inline, card-elevated treatment

### 3. `ObjectivesListComponent` + `ObjectiveFormComponent` SCSS
- Table: DataTable theme (automatic from F-TH-08)
- Code column: accent code-tag style
- Filter dropdown: themed input
- Form modal: card-elevated, themed inputs

### 4. `KpiListComponent` + `KpiFormComponent` SCSS
- Table: DataTable theme
- Form modal: card-elevated

### 5. `KpiTargetsComponent` SCSS
- 12-month grid: CSS Grid, 4 cols desktop, 3 tablet, 2 mobile
- Each month cell: card treatment (`var(--sf)` bg, `var(--bd)` border, `var(--r-md)` radius)
- Month name header: `font-size: 10px; font-weight: 700; color: var(--t3); text-transform: uppercase`
- Target value input: themed input
- Year selector: pill buttons (`var(--sf)` bg, active gets `rgba(var(--ar), 0.08)` bg + `var(--accent)` color)
- Save button: neon primary

## Rules
- SCSS only per component
- Admin sidebar: if restructure needed for the sidebar layout, add CSS classes only
- No changes to CRUD logic, data, or navigation
