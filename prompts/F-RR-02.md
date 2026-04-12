# F-RR-02 — Risk Register List Component (Inside Project Detail)

## Context
F-RR-01 created models, service, and routing. This prompt builds the risk list component that displays inside the Project Detail page, alongside existing milestones and action items sections.

## Requirements

### 1. Create `RiskRegisterSectionComponent` in `src/app/features/projects/risk-register-section/`

Standalone component. Inputs: `projectId: string`.

**Layout:**
- Section header: "Risk Register" with risk count badge and "Add Risk" button (visible when user has `Projects.Edit` permission)
- Stats row: 4 small stat cards showing Critical (red), High (orange), Medium (yellow), Open (blue) counts — use `ProjectRiskStats`
- Risk matrix mini-visual: 5×5 probability/impact grid showing dot indicators where risks exist (optional enhancement — build a simple colored grid)
- Filterable table of risks:
  - Columns: Risk Code, Title, Category, Risk Score (with color-coded rating badge), Status (badge), Owner, Due Date, Actions
  - Filters: Status dropdown, Rating dropdown, Category dropdown
  - Pagination using existing pagination pattern
  - Sort by RiskScore descending by default
- Each row has action buttons: View (eye icon), Edit (pencil icon), Delete (trash icon) — visibility controlled by permissions
- Color coding for RiskRating: Critical = `#dc3545` (red), High = `#fd7e14` (orange), Medium = `#ffc107` (yellow/amber), Low = `#198754` (green)
- Color coding for Status: Open = `#0d6efd` (blue), Mitigating = `#6f42c1` (purple), Accepted = `#20c997` (teal), Transferred = `#6c757d` (gray), Closed = `#198754` (green)

**Behavior:**
- On init, load risk stats and first page of risks
- "Add Risk" opens the risk form component as a Bootstrap modal
- Edit opens the same modal pre-populated
- Delete shows the existing `ConfirmDialogComponent`
- After create/edit/delete, refresh the list and stats
- Excel export button: exports visible risks to `.xlsx` using the existing `xlsx` library pattern
- PDF print button: triggers `window.print()` with print-friendly styles

### 2. Integrate into `ProjectDetailComponent`
Add `<app-risk-register-section [projectId]="project.id">` into the project detail page, as a new tab or section after the milestones section. This is the ONLY modification to an existing component.

### 3. Create `RiskRatingBadgeComponent` in `src/app/shared/components/risk-rating-badge/`
Standalone component. Input: `rating: string`. Displays a color-coded badge (same pattern as `StatusBadgeComponent` and `PriorityBadgeComponent`).

## Rules
- Standalone components only
- Bootstrap 5 classes for layout (cards, tables, badges, buttons)
- SCSS with variables, sapphire blue `#0F52BA` accent
- White/light surfaces only
- Use `PermissionStateService.hasPermission()` for button visibility
- Use ngx-toastr for success/error messages
- Responsive — table scrolls horizontally on mobile
