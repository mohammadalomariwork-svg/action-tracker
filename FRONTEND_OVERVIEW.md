# KU Action Tracker - Frontend Overview

## Tech Stack

- **Framework:** Angular 20.3 (Standalone Components, no NgModules)
- **Build Tool:** Vite with Angular CLI 20.3.17
- **UI Framework:** Bootstrap 5.3.3 + Bootstrap Icons 1.13.1
- **Charts:** ng2-charts 6.0.1 / Chart.js 4.4.7
- **Notifications:** ngx-toastr 19.0.0
- **Excel Export:** xlsx 0.18.5
- **Date Utils:** date-fns 4.1.0
- **Auth:** @azure/msal-browser + @azure/msal-angular (Azure AD / MSAL)

---

## Authentication

### Dual Authentication Flow
1. **Azure AD (MSAL):** User clicks login -> MSAL popup -> Azure login -> redirect to `/auth_fallback` -> `AuthService.loginWithAzureAd(msalToken)` -> backend validates -> JWT tokens stored in localStorage.
2. **Local (Email/Password):** User enters credentials -> `AuthService.login()` -> backend validates -> JWT tokens stored in localStorage.

### Token Management
- `access_token` - JWT Bearer token for API requests
- `refresh_token` - used to obtain new access tokens
- `auth_user` - user info JSON (userId, email, displayName, loginProvider, roles)

### Guards
| Guard | Purpose |
|-------|---------|
| `authGuard` | Checks JWT validity, redirects to `/login` if expired |
| `loginGuard` | Prevents authenticated users from accessing login page |
| `permissionGuard` | Checks effective permissions (area + action), redirects to `/access-denied` |
| `adminGuard` | Requires Admin role |
| `roleGuard` | Requires a specific role |

### HTTP Interceptors
| Interceptor | Purpose |
|-------------|---------|
| `authInterceptor` | Attaches Bearer token; handles 401 with token refresh + request queue |
| `loadingInterceptor` | Shows/hides global loading bar |
| `errorInterceptor` | Displays toast errors for failed requests |

---

## Routing Structure

| Route | Component | Permission Required |
|-------|-----------|-------------------|
| `/` | Redirects to `/dashboard` | - |
| `/login` | Login page | loginGuard (redirect if authed) |
| `/unauthorized` | Unauthorized page | - |
| `/access-denied` | Access denied page | - |
| `/auth_fallback` | MSAL redirect handler | - |
| `/dashboard` | Team Dashboard | Dashboard.View |
| `/management` | Management Dashboard | ActionItems.View |
| `/actions` | My Actions list | - |
| `/actions/new` | Create action | - |
| `/actions/:id/edit` | Edit action | - |
| `/actions/:id/view` | Action detail | - |
| `/reports` | Reports & CSV export | Reports.View |
| `/workspaces` | Workspace list | Workspaces.View |
| `/workspaces/new` | Create workspace | Workspaces.Create |
| `/workspaces/edit/:id` | Edit workspace | Workspaces.Edit |
| `/workspaces/:id` | Workspace detail | Workspaces.View |
| `/projects/my` | My Projects | - |
| `/projects/new` | Create project | Projects.Create |
| `/projects/edit/:id` | Edit project | Projects.Edit |
| `/projects/:id` | Project detail | - |
| `/projects/:projectId/milestones/:milestoneId` | Milestone detail | - |
| `/profile` | User profile | - |
| `/admin` | Admin panel | adminGuard |
| `/admin/org-chart` | Org units | OrgChart.View |
| `/admin/objectives` | Strategic objectives | StrategicObjectives.View |
| `/admin/kpis` | KPI management | KPIs.View |
| `/admin/kpis/:kpiId/targets` | KPI monthly targets | KPIs.View |
| `/admin/permissions/roles` | Role permission matrix | PermissionsManagement.View |
| `/admin/permissions/users` | User permission overrides | PermissionsManagement.View |
| `/admin/roles` | Roles list | Roles.View |
| `/admin/roles/:roleName/permissions` | Edit role permissions | Roles.Edit |
| `/admin/roles/:roleName/users` | Assign users to role | Roles.Assign |
| `/admin/users` | User management | UserManagement.View |

---

## Components (57 Total)

### Layout
| Component | Description |
|-----------|-------------|
| `LayoutComponent` | Main layout with header, footer, router outlet |
| `HeaderComponent` | Navigation header with dynamic menu based on permissions |
| `FooterComponent` | Application footer |
| `LoadingBarComponent` | Global loading indicator |

### Shared UI Components
| Component | Description |
|-----------|-------------|
| `BreadcrumbComponent` | Navigation breadcrumb trail |
| `PageHeaderComponent` | Page title and header bar |
| `StatusBadgeComponent` | Color-coded status indicator |
| `PriorityBadgeComponent` | Color-coded priority indicator |
| `ProgressBarComponent` | Visual progress bar (0-100%) |
| `DataTableComponent` | Reusable sortable/paginated data table |
| `ConfirmDialogComponent` | Confirmation modal dialog |
| `CommentsSectionComponent` | Comments display with CRUD, high-importance flag |
| `DocumentsSectionComponent` | File upload, download, and delete |
| `KpiCardComponent` | KPI metric display card |

### Dashboard
| Component | Description |
|-----------|-------------|
| `TeamDashboardComponent` | KPI stat cards (total, completion rate, on-time rate, escalations, overdue), workspace summary, my actions, recent actions, status breakdown |
| `ManagementDashboardComponent` | Team workload, at-risk items, status breakdown, critical actions |

### Action Items
| Component | Description |
|-----------|-------------|
| `ActionListComponent` | "My Actions" list with filters (status, priority, workspace), pagination, export to Excel, PDF print, "Created by Me" section, stats cards |
| `ActionFormComponent` | Create/edit form with assignee dropdown, status, priority, dates, escalation |
| `ActionDetailComponent` | Detail view with comments and documents sections |

### Workspaces
| Component | Description |
|-----------|-------------|
| `WorkspaceListComponent` | All workspaces with filters, stats (projects, milestones, action items), export/print |
| `WorkspaceFormComponent` | Create/edit workspace with org unit and admin user selection (offcanvas drawer) |
| `WorkspaceDetailComponent` | Detail view with projects, milestones, action items, stats, export/print |

### Projects
| Component | Description |
|-----------|-------------|
| `MyProjectsComponent` | Role-based filtered projects list |
| `ProjectFormComponent` | Create/edit with workspace, type (Strategic/Operational), status, priority, manager, sponsors, dates, budget, strategic objective linking |
| `ProjectDetailComponent` | Detail view with Gantt chart, milestones grid (inline action items), completion stats, export/print |
| `MilestoneSectionComponent` | Milestone list within a project |
| `MilestoneDetailComponent` | Milestone detail with action items, export/print |

### Admin Panel
| Component | Description |
|-----------|-------------|
| `AdminPanelComponent` | Admin dashboard home |
| `OrgChartListComponent` | Org unit hierarchy list |
| `OrgUnitFormComponent` | Create/edit org unit |
| `ObjectivesListComponent` | Strategic objectives list (paginated, filterable by org unit) |
| `ObjectiveFormComponent` | Create/edit strategic objective |
| `KpiListComponent` | KPI management list |
| `KpiFormComponent` | Create/edit KPI |
| `KpiTargetsComponent` | Monthly KPI targets management (12 months/year) |

### Permissions
| Component | Description |
|-----------|-------------|
| `RolePermissionsPageComponent` | Role permission matrix editor (12 areas x 7 actions) |
| `RolePermissionMatrixComponent` | Permission matrix grid |
| `RolePermissionRowComponent` | Single permission row in matrix |
| `UserOverridesPageComponent` | User-level permission overrides |
| `UserOverrideFormComponent` | Create/edit user permission override |
| `UserOverridesListComponent` | List of user overrides |
| `UserEffectivePermissionsSummaryComponent` | Effective permissions summary |

### Roles
| Component | Description |
|-----------|-------------|
| `RolesListPageComponent` | All roles with user count |
| `RolePermissionsPageComponent` | Edit role permissions |
| `RoleUsersPageComponent` | Assign users to roles, assign org units |

### User Management
| Component | Description |
|-----------|-------------|
| `UserListComponent` | All registered users with pagination and search |
| `RegisterAdUserComponent` | Register existing Azure AD user in system |
| `RegisterExternalUserComponent` | Register local (non-AD) user with email/password |

### Auth
| Component | Description |
|-----------|-------------|
| `LoginComponent` | Login page (supports both AD and local auth) |
| `UnauthorizedComponent` | Unauthorized error page |
| `AccessDeniedComponent` | Access denied error page |

---

## Services & API Integration

### Core Services

| Service | Key Endpoints |
|---------|--------------|
| `AuthService` | `POST /auth/login`, `POST /auth/azure-login`, `POST /auth/refresh-token`, `POST /auth/logout` |
| `ActionItemService` | Full CRUD on `/action-items`, plus `/my-actions`, `/created-by-me`, `/my-stats`, `/assignable-users`, `/process-overdue`, comments CRUD |
| `DashboardService` | `GET /dashboard/kpis`, `/management`, `/team-workload`, `/status-breakdown` |
| `ReportService` | `GET /reports/export/csv` |
| `DocumentService` | `GET/POST/DELETE /documents`, `GET /documents/:id/download` |
| `CommentService` | CRUD on entity comments |
| `ProfileService` | User profile operations |
| `LoadingService` | Global loading state (BehaviorSubject) |
| `ToastService` | Toast notifications via ngx-toastr |

### Feature Services

| Service | Key Endpoints |
|---------|--------------|
| `WorkspaceService` | Full CRUD on `/workspaces`, plus `/summary`, `/stats`, `/org-units`, `/active-users` |
| `ProjectService` | Full CRUD on `/projects`, plus `/stats`, `/strategic-objectives-for-workspace/:id` |
| `MilestoneService` | Full CRUD on `/projects/:id/milestones`, plus `/stats`, `/baseline` |
| `PermissionCatalogService` | Permission areas and actions catalog |
| `PermissionStateService` | `GET /users/me/effective-permissions`, `GET /users/me/org-units`; manages permission state in BehaviorSubject, loads on APP_INITIALIZER |
| `RolePermissionService` | Role-level permission assignment |
| `UserPermissionService` | `GET /users/:id/effective-permissions`, CRUD on `/users/:id/permission-overrides` |
| `KpiService` | Full CRUD on `/kpis`, plus `/targets/bulk-upsert`, `/:id/targets` |
| `StrategicObjectiveService` | Full CRUD on `/strategicobjectives`, plus `/by-orgunit/:id` |
| `OrgUnitService` | Org unit CRUD |
| `RoleManagementService` | Role listing and user assignment |
| `UserManagementService` | `GET/POST /users`, `/register-external`, `/register-ad`, `/search-employees`, `PATCH /:id/role`, `PATCH /:id/org-unit` |

---

## Models & Interfaces

### Core Models
| Model | Key Fields |
|-------|-----------|
| `ActionItem` | id, actionId (ACT-001), title, description, workspaceId, projectId, milestoneId, isStandalone, priority, status, startDate, dueDate, progress (0-100), isEscalated |
| `ActionStatus` (enum) | ToDo, InProgress, InReview, Done, Overdue |
| `ActionPriority` (enum) | Low, Medium, High, Critical |
| `DashboardKpi` | totalActions, completionRate, onTimeRate, escalations, criticalHighCount, overdueCount |
| `AuthResponse` | accessToken, refreshToken, user info |
| `CommentInfo` | id, content, authorUserId, isHighImportance, createdAt |
| `DocumentInfo` | id, name, fileName, contentType, fileSize, createdAt |
| `ApiResponse<T>` | success, data, message |
| `PagedResult<T>` | items, totalCount, page, pageSize |

### Feature Models
| Model | Key Fields |
|-------|-----------|
| `Workspace` | id, title, organizationUnit, orgUnitId, isActive, admins |
| `WorkspaceSummary` | totalWorkspaces, activeWorkspaces, strategicProjects, operationalProjects |
| `Project` | id, projectCode (PRJ-YYYY-001), name, workspaceId, projectType, projectStatus, priority, projectManagerUserId, sponsors, budget, isBaselined |
| `ProjectType` (enum) | Operational, Strategic |
| `ProjectStatus` (enum) | Draft, Active, OnHold, Completed, Cancelled |
| `Milestone` | id, milestoneCode (MS-YYYY-001), name, projectId, sequenceOrder, status, completionPercentage, isDeadlineFixed, approverUserId, baseline dates |
| `MilestoneStatus` (enum) | NotStarted, InProgress, Completed, Delayed, Cancelled |
| `PermissionArea` (enum) | Dashboard, Workspaces, Projects, Milestones, ActionItems, StrategicObjectives, KPIs, Reports, OrgChart, UserManagement, PermissionsManagement, Roles |
| `PermissionAction` (enum) | View, Create, Edit, Delete, Approve, Export, Assign |
| `Kpi` | id, kpiNumber, name, calculationMethod, period, unit, strategicObjectiveId |
| `StrategicObjective` | id, objectiveCode, statement, description, orgUnitId |
| `OrgUnit` | id, name, code, level, parentId |

---

## Key Features

### Dashboards
- **Team Dashboard:** 6 KPI stat cards (total actions, completion rate, on-time rate, escalations, critical/high count, overdue), workspace summary cards, My Actions list, Recent Actions list
- **Management Dashboard:** Team workload with completion %, at-risk items with severity, status breakdown, critical actions list

### Action Items
- Create standalone or linked to project/milestone
- Multiple assignees per action item
- Status workflow: ToDo -> InProgress -> InReview -> Done (+ Overdue auto-processing)
- Priority levels: Low, Medium, High, Critical
- Progress tracking (0-100%)
- Escalation with reason tracking
- Threaded comments with high-importance flag
- File attachments (upload, download, delete)
- "My Actions" and "Created by Me" sections
- Excel export and PDF print with filters
- Soft delete and restore

### Projects
- Types: Strategic (linked to strategic objectives) and Operational
- Status workflow: Draft -> Active -> OnHold -> Completed/Cancelled
- Project Manager and multiple Sponsors assignment
- Budget tracking (AED currency)
- Baseline date snapshots for variance tracking
- Interactive Gantt chart with milestone bars and hover tooltips
- Milestones with sequence ordering, fixed/flexible deadlines, completion %, approver
- Inline action items per milestone in project detail
- Milestone validation: block activation when milestones/items incomplete
- Excel export and PDF print

### Workspaces
- Organizational container scoped to org unit
- Multiple admin users per workspace
- Stats: project count, milestone count, action item counts
- Create/edit via offcanvas side drawer
- Soft delete and restore

### Admin Panel
- **Org Chart:** Hierarchical org unit management
- **Strategic Objectives:** Tied to org units, auto-generated codes (SO-001)
- **KPIs:** Tied to strategic objectives, monthly targets (12 months/year), bulk upsert, calculation methods

### Permission System
- **12 Permission Areas** x **7 Permission Actions** = 84 possible permissions
- Role-based permission matrix editor
- User-level permission overrides (grant or deny)
- Org unit scoping for data isolation
- Dynamic header navigation based on permissions
- Route guards enforce permission checks
- Permissions pre-loaded via APP_INITIALIZER on hard refresh

### Role & User Management
- List all roles with user counts
- Assign/remove users from roles
- Assign org units to individual users
- Register AD users (search employee directory)
- Register external/local users with email/password
- Paginated user list with search

### Export & Reporting
- Excel export (.xlsx) on all major pages (actions, workspaces, projects, milestones)
- PDF print (browser Ctrl+P friendly layouts)
- CSV export via reports page with filters

### Charts
- Pie charts: Status breakdown (color-coded by status)
- Bar charts: Team workload, completion rates
- Gantt chart: Project timelines with milestones

---

## Environment Configuration

| Key | Value |
|-----|-------|
| API Base URL | `https://localhost:7135/api` |
| Azure AD Client ID | Configured in `environment.ts` |
| Azure AD Authority | `https://login.microsoftonline.com/{tenantId}` |
| MSAL Redirect URI | `http://localhost:4200/auth_fallback` |
| MSAL Scopes | `api://{clientId}/access_as_user` |
| Cache Location | localStorage |
