# F-TH-10 — Action Items: ActionList, ActionForm, ActionDetail

## Context
Apply dark neon theme to all action item pages. Only SCSS and minimal HTML class additions.

## Requirements

### 1. `ActionListComponent` SCSS
- Page header: themed PageHeader (already styled in F-TH-07)
- Stats row: KpiCard grid (2→3→6 col responsive) for Total, In Progress, Completed, Overdue, Critical
- Filter bar: card wrapper, themed inputs from F-TH-06
- Action items table: DataTable theme from F-TH-08 applies automatically
- "Created by Me" section: separate card below, same table treatment
- Export/print buttons: ghost button style
- "New Action" button: neon primary style
- Mobile: table→card view swap at 600px

### 2. `ActionFormComponent` SCSS
- If modal: `var(--el)` bg, `var(--r-xl)` radius, `backdrop-filter: blur(24px)`, slide-up animation
- If full page: single card wrapper, max-width 800px centered
- Form layout: `.form-grid` — 2 columns desktop, 1 column mobile (≤600px)
- All inputs/selects: themed from F-TH-06 (automatic)
- Assignee multi-select / tags: `var(--sf)` bg chips, `var(--accent)` border on focus
- Buttons row: flex end, Cancel ghost + Save neon primary
- Mobile: buttons stack full-width

### 3. `ActionDetailComponent` SCSS
- Two-column layout: `.detail-grid` — `grid-template-columns: 5fr 2fr` on desktop, stacked mobile (≤900px)
- Main column cards: Description, Comments (themed in F-TH-08), Documents (themed in F-TH-08)
- Side column cards: Info panel with `info-row` key-value pairs (label `var(--t3)`, value `var(--t1)`), Assignees list (avatar circles), Related info (workspace/project links in `var(--accent)`)
- Action buttons: Edit (ghost), Delete (ghost danger), Back link

## Rules
- SCSS only for each component
- No changes to data bindings, form controls logic, validation, or service calls
- If HTML needs a CSS class added (e.g., `.detail-grid` on a div), that is the ONLY HTML change permitted
- All existing `*ngIf`, `*ngFor`, `[formControl]`, `(click)` handlers remain untouched
