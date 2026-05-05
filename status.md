# KU Action Tracker — Implementation Status

> Last updated: 2026-05-04

---

## 1. Authentication & Authorization

### Azure AD SSO
- Login via Microsoft Azure AD (MSAL) with federated token exchange
- Local login fallback with email/password
- JWT access tokens with refresh token rotation
- Automatic user provisioning from Azure AD on first login

### Permission System
- **PermissionArea / PermissionAction / AreaActionMapping** catalog model
- **Role-based permissions** — matrix UI to assign permissions per role
- **User-level overrides** — grant or deny individual permissions on top of role defaults
- **Effective permission resolution** — merges role + user overrides at runtime
- **Angular `*hasPermission` directive** — conditionally renders UI elements
- **PermissionStateService** — caches effective permissions and visible org units for the session; clears on logout
- **Backend policy-based authorization** on every endpoint (e.g. `ActionItemsView`, `ProjectsEdit`)
- **Org-unit scoping** — users only see data belonging to their assigned org unit hierarchy

### Policies Implemented
Actions, Projects, Milestones, KPIs, Strategic Objectives, Org Chart, Workspaces, User Management, Roles, Permissions Management, Reports, Dashboard, Notifications, Email Templates, Risks

---

## 2. Organizational Structure

### Org Units
- Hierarchical org chart up to 10 levels deep
- Tree view and flat paged list in admin UI
- CRUD with soft-delete and restore
- Cascade soft-delete for child units
- Users assigned to org units for data scoping

### Strategic Objectives
- Linked to org units
- Auto-generated `ObjectiveCode`
- CRUD with soft-delete/restore
- Referenced by strategic projects and KPIs

---

## 3. Workspaces

- Container for projects and action items, associated with an org unit
- Workspace admins assigned per workspace via searchable multi-select (ng-select) — searches name, email, and org unit
- Summary stats (project counts, open action items)
- Scoped to user's visible org units (Level-2 ancestor)
- Search, sort, pagination, org-unit filter dropdown
- Archive/restore (soft-delete)
- Export to Excel and Print to PDF
- Create/edit via right-side offcanvas drawer

---

## 4. Projects

### Project Management
- **Project types:** Strategic and Operational
- **Statuses:** Draft, Active, On Hold, Completed, Cancelled, **Pending Approval**
- Auto-generated `ProjectCode` (PRJ-YYYY-NNN)
- Fields: budget (AED), priority, sponsors, project manager, planned/actual dates
- Linked to workspace and (optionally) a strategic objective
- My Projects page with role-based filtering
- Activation validation: blocks if milestones or action items are missing
- Soft-delete cascades to milestones and action items

### Milestones
- Nested under projects, ordered by sequence
- Auto-generated `MilestoneCode` (MS-YYYY-NNN)
- **Statuses:** NotStarted, InProgress, AtRisk, Completed
- **Project phase** (mandatory): Initiation, Planning, Execution, Monitoring & Controlling, Closing
- Completion percentage tracking (0-100)
- Approver user for sign-off
- Baseline locking — saves planned dates as baseline for variance tracking
- Status transition validation (Active/Completed checks)
- Export to Excel and Print to PDF

### Gantt Chart
- Interactive Gantt visualization of milestones and action items
- Color-coded bars by status
- Hover tooltips with details
- Positioned below project details section

### Risk Register
- Per-project risk management
- Auto-generated `RiskCode` (RISK-NNN per project)
- Probability x Impact scoring (1-5 scale, risk score 1-25)
- **Risk ratings:** Critical (20-25), High (12-19), Medium (5-11), Low (1-4)
- **Statuses:** Open, Mitigated, Closed
- Mitigation plan, contingency plan, and notes fields
- Risk owner assignment
- Stats summary endpoint
- Soft-delete/restore

---

## 5. Action Items

- Core tracked entity across the system
- Auto-generated `ActionId` (ACT-NNN)
- **Statuses:** ToDo, InProgress, InReview, Done, Overdue, Deferred, Cancelled
- Priority levels, start/due dates, progress (0-100 auto-clamped)
- Can be standalone or linked to workspace/project/milestone
- Multi-user assignee support
- Escalation tracking
- Created-by tracking (`CreatedByUserId`)
- **My Actions page:**
  - Scoped to current user (assigned + created by me sections)
  - 7 stat cards (total, by status, overdue, etc.)
  - Show-deleted toggle
  - Create/edit via right-side offcanvas panel
  - Mobile-responsive card layout
- Overdue processing endpoint (admin)
- Comments on action items
- Soft-delete/restore
- Export to Excel and Print to PDF

---

## 6. KPIs

- Linked to strategic objectives
- Auto-numbered per objective
- Measurement periods: Monthly, Quarterly, SemiAnnual, Yearly
- Calculation method and unit fields
- **Targets:** per-month target and actual values with notes
- Bulk upsert for monthly targets by year
- CRUD with soft-delete/restore

---

## 7. Dashboards

### Team Dashboard
- Personal action items due this week
- Recent activity feed
- KPI metrics (completion rate, on-time delivery)
- Status breakdown (horizontal bar chart via Chart.js)
- Workspace summary statistics
- 6 equal-height stat cards

### Management Dashboard
- Executive-level view (Admin/Manager roles only)
- Auto-refresh every 30 seconds
- Team workload distribution (horizontal bar chart)
- At-risk items with severity levels
- Critical action items
- Status breakdown (doughnut chart)
- Organization-wide KPI metrics

---

## 8. Reports & Export

### Reports Page
- Global KPI metrics: total actions, completion rate, on-time delivery, escalations, overdue count
- Action items breakdown by category and priority (bar + doughnut charts)
- Team member performance statistics
- Filterable by status, priority, assignee, date range
- CSV export with applied filters

### Export Capabilities (across features)
- **Export to Excel (XLSX):** Workspaces, projects, milestones, my actions
- **Print to PDF:** Workspaces, projects, milestones, my actions

---

## 9. Comments & Documents

### Comments
- Generic comment system (polymorphic: works on any entity type)
- High-importance flag
- Author tracking with timestamps
- Also available as action-item-specific comments

### Documents
- Generic document attachment (polymorphic: any entity type)
- File upload with 10 MB limit
- Download endpoint
- Tracks uploader, file size, content type

---

## 10. Notifications

- In-app notification system
- Grouped by date (Today, Yesterday, This Week, Earlier)
- Filter by type: ActionItem, Project, Milestone, Workspace, Risk, Kpi, StrategicObjective, System
- Filter by read/unread status
- Mark as read (individual + mark all)
- Delete individual or batch delete read notifications
- Unread count endpoint for header bell icon
- Paginated with "load more"

---

## 11. User Management

- Paginated, sortable user list with search
- Inline role and org-unit assignment
- **Registration flows:**
  - Azure AD user registration (auto-synced from directory)
  - External/local user registration (username/password)
- User activation/deactivation
- KU employee directory search integration
- Profile page showing display name, email, login provider, roles

---

## 12. Role Management

- Role CRUD with inline creation dialog
- Role-specific user listing and assignment
- Permission matrix per role
- Prevents deletion of roles with active users
- 4-card-per-row layout with paginated user lists

---

## 13. Email Templates

- System-level email template management
- Template key-based lookup
- Editable subject and HTML body
- Active/inactive toggle
- Delivery logging for audit

---

## 14. UI / UX

- **Theme:** Fluent / Palantir design aesthetic
- **Dark and light mode** toggle (persisted)
- **Login page** with KU branding and animated particle background
- **Header:** Dynamic nav with permission-based link visibility, theme toggle, notification bell, user menu, mobile hamburger drawer
- **Footer** with KU logo
- **Loading bar** on route transitions
- **Browser title:** KU Action Tracker with KU favicon
- **Responsive:** Mobile card layouts for actions and lists
- **Standalone components** throughout (Angular best practice)
- **Angular signals** for state management

---

## 15. Action Item Workflow

### Date Freeze
- After creation, start/due dates on standalone action items are locked
- Editing dates requires submitting a date change request with justification
- Lock icon (🔒) displayed on frozen date fields in the UI
- Non-standalone (project/milestone-linked) items are unaffected

### Status Change Approval
- Moving standalone items to terminal statuses (Done, Deferred, Cancelled) requires approval
- Direct transition ToDo → InProgress is allowed without workflow
- Two new statuses added: Deferred and Cancelled
- Status change requests include justification reason

### Approval Flow
- **My Approvals page** (`/approvals`) with two tabs: Pending Reviews and My Requests
- Stat cards showing pending date/status change counts
- Reviewers: action item creator + direct manager of assignee(s)
- Manager lookup via KuEmployeeInfo (supervisor email/name)
- Approve with optional comment; reject with required comment
- Approved changes are applied automatically (dates updated, status changed)

### Escalation Workflow
- Escalating an action item notifies creator + direct manager(s) of assignees
- Creator/manager can give direction (creates high-importance comment)
- Direction notification sent to all assignees

### Workflow Notifications
- 8 notification action types: DateChangeRequested/Approved/Rejected, StatusChangeRequested/Approved/Rejected, ActionItemEscalated, EscalationDirectionGiven
- In-app notifications via AppNotification + SignalR real-time push
- Email notifications using 5 new templates (DateChangeRequested, DateChangeReviewed, StatusChangeRequested, StatusChangeReviewed, DirectionGiven)
- Workflow-specific toast popups (info/success/warning/error by event type)
- Clickable toasts navigate to relevant page
- Header badge showing pending approval count (refreshes every 60s + on SignalR events)
- Deduplication: same user in multiple roles receives one notification per event

### Shared Components
- Reusable WorkflowReviewDialog component (used in My Approvals and Action Detail)
- WorkflowStateService for shared pending count state
- Workflow request history section in action form and detail views

---

## 16. Project Approval Workflow

### Project Creation Notifications
- On project creation, email + in-app notification sent to direct line manager and all project sponsors

### Draft Phase & Submit for Approval
- Project manager can add/edit milestones and action items while project is in Draft
- "Submit for Approval" button on project detail (visible to PM when status is Draft)
- Opens modal for justification reason
- Project status transitions from Draft → PendingApproval
- Notification sent to all sponsors + PM's direct line manager

### Approval Gate
- Any single sponsor or the PM's direct line manager can approve/reject
- Approve: project transitions to Active, ActualStartDate set, IsBaselined set, milestone dates baselined
- Reject: project returns to Draft with reviewer comment
- All other pending requests auto-closed on approval

### Date Freeze (Post-Approval)
- Once project leaves Draft status, the following dates become read-only:
  - Project: PlannedStartDate, PlannedEndDate
  - Milestones: PlannedStartDate, PlannedDueDate
  - Action items (linked to project): StartDate, DueDate
- New milestones and action items cannot be added to non-Draft projects
- Milestones and action items cannot be deleted from non-Draft projects
- Backend enforcement via service-level guards + frontend UI enforcement (disabled inputs, lock icons, hidden buttons)

### Approval Notifications
- Email + in-app notification to the project manager with reviewer's decision and comment
- `ProjectApproval.Requested` and `ProjectApproval.Reviewed` email templates

### UI Components
- "Submit for Approval" button and justification modal on project detail page
- "Pending Approval" banner with inline approve/reject buttons for reviewers
- Approval History section (collapsible table) on project detail page
- "Project Approvals" tab on My Approvals page with pending reviews and submitted requests
- Review modal for approve/reject with comment
- Header badge includes project approval count in total
- "Create New Project" button on My Projects page (permission-gated)
- PendingApproval status badge (amber) throughout project list and detail views
- Date inputs locked with disabled state when project is not in Draft

### Entity Model
- `ProjectApprovalRequest` entity with FK to Project
- `ProjectApprovalStatus` enum: Pending, Approved, Rejected
- `PendingApproval` added to `ProjectStatus` enum

### API Endpoints (ProjectWorkflowController)
- `POST /api/projects/workflow/submit` — submit for approval
- `PUT /api/projects/workflow/requests/{id}/review` — approve or reject
- `GET /api/projects/workflow/project/{id}` — get approval requests for a project
- `GET /api/projects/workflow/pending-reviews` — get pending reviews for current user
- `GET /api/projects/workflow/my-requests` — get user's submitted requests
- `GET /api/projects/workflow/pending-summary` — pending count for header badge
- `GET /api/projects/workflow/can-review/{id}` — check if user can review a project

---

## 17. Not Yet Implemented

- [ ] Audit trail / activity log
- [ ] Advanced search (cross-entity, full-text)
