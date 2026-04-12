# F-TH-12 — Workspaces: WorkspaceList, WorkspaceForm, WorkspaceDetail

## Requirements

### 1. `WorkspaceListComponent` SCSS
- Summary stat cards: KpiCard grid (Total, Active, Strategic, Operational)
- Workspace cards: CSS Grid (3 cols desktop, 2 tablet, 1 mobile), each card `var(--sf)` bg with border, title + org unit + stats (icon+number), admin avatars stacked, active dot indicator
- `.card-glow:hover` on each card
- Export/print: ghost buttons

### 2. `WorkspaceFormComponent` SCSS (offcanvas drawer)
- Offcanvas panel: `var(--el)` bg, `var(--bd)` border-left, `backdrop-filter: blur(24px)`, 420px width (full-width mobile)
- Backdrop: `rgba(0,0,0,0.50); backdrop-filter: blur(4px)`
- Header: title + close button (icon-button style)
- Form: themed inputs from F-TH-06
- Footer buttons sticky: Cancel ghost + Save neon primary
- Slide-in animation from right, 350ms

### 3. `WorkspaceDetailComponent` SCSS
- Header card: workspace name, org unit, admins, active status badge
- Stats row: 4 stat cards
- Tab navigation (if using tabs):
  - `.tabs` container: `var(--sf)` bg, `var(--r-lg)` radius, `var(--bd)` border, 3px padding
  - Tab items: `var(--r-md)` radius, `var(--t3)` color
  - Active tab: `rgba(var(--ar), 0.08)` bg, `var(--accent)` color
  - Hover: `var(--sfh)` bg
- Tab content panels: themed DataTable for each tab (projects, milestones, actions)

## Rules
- SCSS only per component
- Offcanvas restructure: SCSS changes only, keep existing Bootstrap offcanvas logic
- No data, routing, or service changes
