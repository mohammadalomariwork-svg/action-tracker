# F-TH-11 — Projects: MyProjects, ProjectForm, ProjectDetail, Milestones, Gantt

## Context
Apply dark neon theme to all project management pages.

## Requirements

### 1. `MyProjectsComponent` SCSS
- Stats row: KpiCard grid — Total, Active, On Hold, Completed, At Risk
- Filter bar: card wrapper, themed dropdowns
- Projects: if table → DataTable theme. If card grid → use CSS Grid (2 cols desktop, 1 col mobile), each card has code tag, name, status/priority badges, progress bar, project manager name
- Cards: `var(--sf)` bg, `var(--bd)` border, `.card-glow:hover`

### 2. `ProjectFormComponent` SCSS
- Full-page form: single card, max-width 900px centered
- Two-column form grid, single column mobile
- Section dividers: `border-top: 1px solid var(--bd); padding-top: 20px; margin-top: 20px`
- Section titles: `font-size: 13px; font-weight: 700; color: var(--t3); text-transform: uppercase; letter-spacing: 0.06em`
- Strategic objective dropdown section: conditionally visible area with same card bg
- Buttons: Cancel ghost + Save neon primary

### 3. `ProjectDetailComponent` SCSS
- Two-column `.detail-grid` (5:2 desktop, stacked mobile)
- Main column cards: Description, Gantt chart, Milestones section, Risk register section, Comments, Documents
- Side column cards: Project info panel (code, status, priority, type, budget, dates, progress bar), People card (PM avatar + sponsors), Notifications card
- All cards: themed card treatment

### 4. `MilestoneSectionComponent` + `MilestoneDetailComponent` SCSS
- Milestone list: cards or rows with `var(--sf)` bg, sequence number, name, code tag, status badge, completion %, date range
- Inline action items per milestone: compact list rows
- Milestone detail: same two-column detail layout

### 5. Gantt Chart Theming
- Canvas/container background: transparent (card bg shows through)
- Milestone bars: `var(--accent)` gradient for active, `#22c55e` for completed, `#ef4444` for delayed
- Grid lines: `rgba(var(--ar), 0.06)`
- Today line: `rgba(239,68,68,0.50)` dashed vertical
- Tooltips: card-elevated treatment
- Labels: `var(--t2)` color

## Rules
- SCSS only per component (plus adding CSS classes to HTML if needed)
- Do NOT change Gantt chart logic, data processing, or canvas rendering code
- If Gantt uses canvas: update color variables in the component where colors are defined (minimal TS — color values only)
- All form bindings, validators, dropdowns, and conditional logic remain unchanged
