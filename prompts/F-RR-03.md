# F-RR-03 — Risk Form (Create/Edit Modal) + Risk Detail Component

## Context
F-RR-02 created the risk list section inside project detail. This prompt builds the create/edit form modal and a full risk detail view page.

## Requirements

### 1. Create `RiskFormComponent` in `src/app/features/projects/risk-form/`

Standalone component. Used as a Bootstrap modal.

**Inputs:** `projectId: string`, `risk?: ProjectRisk` (null for create, populated for edit)
**Outputs:** `saved: EventEmitter<ProjectRisk>`, `cancelled: EventEmitter<void>`

**Form fields:**
- Title (text input, required, max 300)
- Description (textarea, required, max 2000)
- Category (dropdown, required — populated from `RISK_CATEGORIES` constant)
- Probability Score (1–5 dropdown or range slider with labels: 1=Rare, 2=Unlikely, 3=Possible, 4=Likely, 5=Almost Certain)
- Impact Score (1–5 dropdown or range slider with labels: 1=Negligible, 2=Minor, 3=Moderate, 4=Major, 5=Severe)
- **Live Risk Score Display:** Show the computed `Probability × Impact` value and the derived RiskRating badge in real-time as the user changes probability/impact
- Status (dropdown — from `RISK_STATUSES` constant; default "Open" for new risks)
- Risk Owner (user dropdown — call existing assignable-users endpoint or user search)
- Due Date (date input)
- Mitigation Plan (textarea, max 2000)
- Contingency Plan (textarea, max 2000)
- Notes (textarea, max 2000)

**Behavior:**
- Reactive form with validation matching backend validators
- On create: call `ProjectRiskService.createRisk()`, emit `saved`, show success toast
- On edit: pre-populate all fields, call `ProjectRiskService.updateRisk()`, emit `saved`, show success toast
- On cancel: emit `cancelled`
- Show validation errors inline under each field
- Disable submit button while saving (loading state)

### 2. Create `RiskDetailComponent` in `src/app/features/projects/risk-detail/`

Standalone component. Routed page at `/projects/:projectId/risks/:riskId`.

**Layout:**
- Breadcrumb: Projects > [Project Name] > Risk Register > [Risk Code]
- Page header with risk code and title
- Two-column detail layout:
  - **Left column (8 cols):** Description card, Mitigation Plan card, Contingency Plan card, Notes card
  - **Right column (4 cols):** Risk info card (Category, Probability, Impact, Risk Score with colored badge, Status badge, Owner, Identified Date, Due Date, Closed Date), Created By, timestamps
- **Risk Score Visual:** Show a small 5×5 heat map grid highlighting the intersection of probability and impact for this specific risk
- Comments section using existing `CommentsSectionComponent` (entity type = "ProjectRisk", entity ID = risk ID)
- Documents section using existing `DocumentsSectionComponent` (entity type = "ProjectRisk", entity ID = risk ID)
- Action buttons: Edit (opens modal), Delete (confirm dialog), Back to Project

**Behavior:**
- Load risk detail on init via `ProjectRiskService.getRiskById()`
- Edit/Delete same behavior as in the list
- Permission checks for edit/delete buttons

## Rules
- Standalone components only
- Bootstrap 5, SCSS with variables
- White/light surfaces only
- Reactive forms with Angular validators
- Strongly typed — no `any`
- Use existing shared components (CommentsSectionComponent, DocumentsSectionComponent, ConfirmDialogComponent, PageHeaderComponent, BreadcrumbComponent)
