# KU Action Tracker - Backend Overview

## Tech Stack

- **Framework:** .NET 9 / ASP.NET Core
- **ORM:** Entity Framework Core (SQL Server)
- **Identity:** ASP.NET Core Identity
- **Authentication:** Dual JWT (Local HS256 + Azure AD OIDC)
- **Validation:** FluentValidation
- **Logging:** Serilog (structured logging)
- **API Docs:** Swagger / Swashbuckle

---

## Architecture

Four-layer clean architecture:

```
ActionTracker.API/            -> Controllers, Middleware, Configuration
ActionTracker.Application/    -> Business Logic, DTOs, Service Interfaces, Validators
ActionTracker.Infrastructure/ -> Data Access, EF Core, Auth, Authorization
ActionTracker.Domain/         -> Entities, Enums, Constants
```

---

## Authentication & Authorization

### Dual JWT Authentication

| Scheme | Description |
|--------|-------------|
| `LocalBearer` | HS256 symmetric key signing for local email/password users |
| `AzureAD` | OpenID Connect discovery against Microsoft Entra ID v2.0 |
| `MultiAuth` | Policy scheme that routes to LocalBearer or AzureAD based on `tid` claim presence |

### Authentication Flows
- **Local Login:** `POST /auth/login` -> validates credentials -> returns JWT access + refresh tokens
- **Azure AD Login:** `POST /auth/azure-login` -> validates MSAL token -> auto-provisions user if first login -> returns JWT tokens
- **Token Refresh:** `POST /auth/refresh-token` -> rotates refresh token -> returns new token pair
- **Logout:** `POST /auth/logout` -> revokes all refresh tokens for user

### Authorization Model

**Role-Based Access Control (RBAC)**
- Pre-seeded roles: Admin, Manager, User, Viewer
- ASP.NET Identity roles with role claims in JWT

**Fine-Grained Permissions (RBAC + ABAC Hybrid)**
- Permissions defined as Area.Action pairs (e.g., `Projects.View`, `ActionItems.Delete`)
- **12 Areas:** Dashboard, Workspaces, Projects, Milestones, ActionItems, StrategicObjectives, KPIs, Reports, OrgChart, UserManagement, PermissionsManagement, Roles
- **7 Actions:** View, Create, Edit, Delete, Approve, Export, Assign
- Stored in `RolePermission` (role-level) and `UserPermissionOverride` (user-level) tables
- **Effective Permissions** = Role Permissions + User Overrides (denials take precedence)
- Checked at runtime via `PermissionRequirement` + `PermissionAuthorizationHandler`

**Authorization Policies**
| Policy | Requirement |
|--------|------------|
| `LocalOrAzureAD` | Authenticated via either scheme |
| `AdminOnly` | Admin role |
| `AdminOrManager` | Admin or Manager role |
| `{Area}.{Action}` | Dynamic policies auto-generated from PermissionPolicies constants |

**Org Unit Scoping**
- Users assigned to org unit via `ApplicationUser.OrgUnitId`
- Workspaces linked to org unit via `Workspace.OrgUnitId`
- `IOrgUnitScopeResolver` resolves visible org units (assigned unit + all descendants)
- Controllers filter queries by `VisibleOrgUnitIds`

---

## API Controllers & Endpoints

### AuthController (`/api/auth`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/login` | Local email/password authentication |
| POST | `/azure-login` | Azure AD federated login (validates MSAL token, auto-provisions) |
| POST | `/refresh-token` | Refresh JWT pair with token rotation |
| POST | `/logout` | Revoke all refresh tokens for current user |

### ActionItemsController (`/api/action-items`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated action items (filtered, org-unit scoped) |
| GET | `/my-actions` | Items assigned to current user |
| GET | `/created-by-me` | Items created by current user |
| GET | `/my-stats` | Stats for user's assigned items (total, critical, in-progress, completed, overdue) |
| GET | `/assignable-users` | Users available for assignment dropdown |
| GET | `/{id}` | Single action item by GUID |
| POST | `/` | Create action item (auto-generates ActionId ACT-001) |
| PUT | `/{id}` | Update action item |
| PATCH | `/{id}/status` | Update status (Done sets Progress=100%) |
| DELETE | `/{id}` | Soft delete (requires ActionItems.Delete) |
| PATCH | `/{id}/restore` | Restore soft-deleted item |
| POST | `/process-overdue` | Mark past-due items as Overdue (Admin only) |
| GET | `/{id}/comments` | Get comments for action item |
| POST | `/{id}/comments` | Add comment |
| PUT | `/{actionItemId}/comments/{commentId}` | Update comment |
| DELETE | `/{actionItemId}/comments/{commentId}` | Delete comment |

### WorkspacesController (`/api/workspaces`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All active workspaces (org-unit scoped) |
| GET | `/summary` | Aggregate stats (scoped to user's visible org units) |
| GET | `/{id}` | Workspace details |
| GET | `/{id}/stats` | Workspace statistics (projects, action items, completion rates) |
| GET | `/by-admin/{adminUserId}` | Workspaces for specific admin |
| POST | `/` | Create workspace |
| PUT | `/{id}` | Update workspace |
| GET | `/org-units` | Org units for dropdown |
| GET | `/active-users` | Active users for dropdown |
| DELETE | `/{id}` | Soft delete (sets IsActive=false) |
| PATCH | `/{id}/restore` | Restore deleted workspace |

### ProjectsController (`/api/projects`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated, filtered projects (org-unit scoped) |
| GET | `/{id}` | Single project |
| POST | `/` | Create project (status=Draft) |
| PUT | `/{id}` | Update project |
| GET | `/{id}/stats` | Project statistics (milestones, action items, rates) |
| GET | `/strategic-objectives-for-workspace/{workspaceId}` | Objectives for workspace |
| DELETE | `/{id}` | Soft delete (cascades to milestones/items) |
| PATCH | `/{id}/restore` | Restore project |

### MilestonesController (`/api/projects/{projectId}/milestones`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All milestones for project (ordered by sequence) |
| GET | `/{milestoneId}` | Single milestone |
| POST | `/` | Create milestone |
| PUT | `/{milestoneId}` | Update milestone |
| DELETE | `/{milestoneId}` | Soft delete |
| GET | `/{milestoneId}/stats` | Action-item stats for milestone |
| POST | `/baseline` | Lock milestone dates (save baseline values) |

### DashboardController (`/api/dashboard`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/kpis` | High-level KPI metrics (Admin/Manager) |
| GET | `/management` | Full management dashboard (KPIs, status, workload, at-risk, activity) |
| GET | `/team-workload` | Per-user workload stats |
| GET | `/status-breakdown` | Action items grouped by status |

### UsersController (`/api/users`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | All role names |
| GET | `/` | All users (paginated, searchable, sortable) |
| GET | `/{id}` | Single user |
| POST | `/register-external` | Register local user with username/password |
| POST | `/register-ad` | Register Azure AD user |
| GET | `/search-employees` | Search KU employee directory |
| PUT | `/update-role` | Replace user roles |
| PUT | `/{id}/deactivate` | Deactivate user account |
| PUT | `/{id}/reactivate` | Reactivate user |
| PUT | `/{id}/assign-org-unit` | Assign/unassign org unit to user |
| GET | `/me/org-units` | Org units visible to current user |

### RoleManagementController (`/api/roles`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | All roles with user counts |
| GET | `/{roleName}` | Single role |
| GET | `/{roleName}/users` | Users in role (paginated) |
| GET | `/{roleName}/permissions` | Permission matrix for role |
| POST | `/` | Create new role |
| DELETE | `/{roleName}` | Delete role (fails if users assigned) |
| POST | `/{roleName}/permissions` | Assign permissions (full replace) |
| POST | `/{roleName}/users/assign` | Assign users to role |
| POST | `/{roleName}/users/remove` | Remove users from role |

### OrgUnitsController (`/api/orgunits`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/tree` | Full org chart tree |
| GET | `/` | Paged flat list |
| GET | `/{id}` | Single org unit |
| GET | `/{id}/children` | Direct children |
| POST | `/` | Create org unit |
| PUT | `/{id}` | Update org unit |
| DELETE | `/{id}` | Soft delete (cascades to descendants) |
| POST | `/{id}/restore` | Restore deleted org unit |

### PermissionCatalogController (`/api/permission-catalog`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/areas` | All permission areas |
| GET | `/areas/{id}` | Single area |
| POST | `/areas` | Create area |
| PUT | `/areas/{id}` | Update area |
| DELETE | `/areas/{id}` | Soft delete area |
| GET | `/actions` | All permission actions |
| GET | `/actions/{id}` | Single action |
| POST | `/actions` | Create action |
| PUT | `/actions/{id}` | Update action |
| DELETE | `/actions/{id}` | Soft delete action |
| GET | `/mappings` | All area-action mappings |
| GET | `/mappings/by-area/{areaId}` | Mappings for area |
| POST | `/mappings` | Create mapping |
| DELETE | `/mappings/{id}` | Soft delete mapping |

### RolePermissionsController (`/api/role-permissions`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | All role names |
| GET | `/{roleName}` | Permissions for role |
| GET | `/{id}/detail` | Single role permission |
| GET | `/matrix/{roleName}` | Permission matrix for role |
| POST | `/` | Create role permission |
| PUT | `/{id}` | Update role permission |
| DELETE | `/{id}` | Soft delete role permission |

### UserPermissionsController (`/api/user-permissions`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{userId}/overrides` | Permission overrides for user |
| GET | `/overrides/{id}` | Single override |
| POST | `/overrides` | Create user permission override |
| PUT | `/overrides/{id}` | Update override |
| DELETE | `/overrides/{id}` | Delete override |
| GET | `/{userId}/effective` | Merged effective permissions for user |
| GET | `/me/effective` | Effective permissions for current user |

### StrategicObjectivesController (`/api/strategicobjectives`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paged list (filterable by org unit) |
| GET | `/{id}` | Single objective |
| GET | `/by-orgunit/{orgUnitId}` | Objectives for org unit |
| POST | `/` | Create (auto-generates ObjectiveCode SO-001) |
| PUT | `/{id}` | Update |
| DELETE | `/{id}` | Soft delete |
| POST | `/{id}/restore` | Restore |

### KpisController (`/api/kpis`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paged KPIs (filterable by objective) |
| GET | `/{id}` | Single KPI with targets (filterable by year) |
| GET | `/by-objective/{objectiveId}` | KPIs for objective |
| POST | `/` | Create KPI (auto-assigns KpiNumber per objective) |
| PUT | `/{id}` | Update KPI |
| DELETE | `/{id}` | Soft delete |
| POST | `/{id}/restore` | Restore |
| POST | `/targets/upsert` | Upsert single target for month |
| POST | `/targets/bulk-upsert` | Upsert all monthly targets for KPI/year |
| GET | `/{id}/targets` | All targets for KPI/year |

### CommentsController (`/api/comments`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Comments by entity (entityType + entityId query params) |
| POST | `/` | Add comment to entity |
| PUT | `/{id}` | Update comment (authorization check) |
| DELETE | `/{id}` | Delete comment (authorization check) |

### DocumentsController (`/api/documents`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Documents by entity (entityType + entityId query params) |
| POST | `/` | Upload document (multipart, max 10MB) |
| GET | `/{id}/download` | Download document |
| DELETE | `/{id}` | Delete document |

### ProfileController (`/api/profile`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/me` | Current user's employee profile from KU directory |

### ReportsController (`/api/reports`)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/export-csv` | Export filtered action items to CSV |
| GET | `/summary` | KPI summary statistics |

---

## Database Entities

### Core Entities

#### ActionItem
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ActionId | string | Human-readable code (ACT-001), auto-generated |
| Title | string | Action item title |
| Description | string | Detailed description |
| WorkspaceId | Guid (FK) | Parent workspace (required) |
| ProjectId | Guid? (FK) | Linked project (nullable) |
| MilestoneId | Guid? (FK) | Linked milestone (nullable) |
| IsStandalone | bool | True when not linked to project/milestone |
| Priority | enum | Low, Medium, High, Critical |
| Status | enum | ToDo, InProgress, InReview, Done, Overdue |
| StartDate | DateTime? | Start date |
| DueDate | DateTime | Due date (required) |
| Progress | int | 0-100 (auto-clamped) |
| IsEscalated | bool | Escalation flag |
| CreatedByUserId | string? | User who created the item |
| CreatedAt | DateTime | UTC creation timestamp |
| UpdatedAt | DateTime | UTC last update timestamp |
| IsDeleted | bool | Soft delete flag |
| **Nav:** | | Workspace, Project, Milestone, Assignees, Escalations, Comments |

#### Project
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ProjectCode | string | Auto-generated (PRJ-YYYY-001) |
| Name | string | Project name |
| Description | string | Description |
| WorkspaceId | Guid (FK) | Parent workspace |
| ProjectType | enum | Operational, Strategic |
| ProjectStatus | enum | Draft, Active, OnHold, Completed, Cancelled |
| StrategicObjectiveId | Guid? (FK) | Required when Strategic type |
| Priority | enum | Low, Medium, High, Critical |
| ProjectManagerUserId | string (FK) | Project manager |
| OwnerOrgUnitId | Guid? (FK) | Owning department/org unit |
| PlannedStartDate | DateTime | Planned start |
| PlannedEndDate | DateTime | Planned end |
| ActualStartDate | DateTime? | Set when status changes to Active |
| ApprovedBudget | decimal? | Budget amount (AED) |
| Currency | string | Always "AED" |
| IsBaselined | bool | Locks dates when true |
| IsDeleted | bool | Soft delete (cascades to milestones/items) |
| **Nav:** | | Workspace, StrategicObjective, ProjectManager, OwnerOrgUnit, Sponsors |

#### Milestone
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| MilestoneCode | string | Auto-generated (MS-YYYY-001) |
| Name | string | Milestone name |
| ProjectId | Guid (FK) | Parent project |
| SequenceOrder | int | Display order |
| PlannedStartDate | DateTime | Planned start |
| PlannedDueDate | DateTime | Planned due date |
| ActualCompletionDate | DateTime? | Actual completion |
| IsDeadlineFixed | bool | Hard deadline flag |
| Status | enum | NotStarted, InProgress, Completed, Delayed, Cancelled |
| CompletionPercentage | decimal | 0-100% |
| ApproverUserId | string? | Formal sign-off approver |
| BaselinePlannedStartDate | DateTime? | Locked baseline start |
| BaselinePlannedDueDate | DateTime? | Locked baseline due |
| IsDeleted | bool | Soft delete flag |
| **Nav:** | | Project, Approver |

#### Workspace
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key (NEWID()) |
| Title | string(200) | Workspace title |
| OrganizationUnit | string(200) | Organization unit name |
| OrgUnitId | Guid? (FK) | Org unit for access scoping |
| IsActive | bool | Active flag (default true) |
| **Nav:** | | Admins (WorkspaceAdmin), Projects, ActionItems |

#### OrgUnit
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Name | string | Org unit name |
| Code | string? | Optional code |
| Level | int | Hierarchy depth |
| ParentId | Guid? (FK) | Parent org unit (tree structure) |
| IsDeleted | bool | Soft delete (cascades to descendants) |
| **Nav:** | | Parent, Children, StrategicObjectives |

#### StrategicObjective
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| ObjectiveCode | string | Auto-generated (SO-001) |
| Statement | string | Objective statement |
| OrgUnitId | Guid (FK) | Scoped to org unit |
| IsDeleted | bool | Soft delete |
| **Nav:** | | OrgUnit, Kpis |

#### Kpi
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| KpiNumber | int | Auto-assigned per objective |
| Name | string | KPI name |
| CalculationMethod | string | How the KPI is measured |
| Period | enum | Monthly, Quarterly, SemiAnnual, Yearly |
| Unit | string? | Measurement unit |
| StrategicObjectiveId | Guid (FK) | Parent objective |
| IsDeleted | bool | Soft delete |
| **Nav:** | | StrategicObjective, Targets (KpiTarget) |

#### KpiTarget
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| KpiId | Guid (FK) | Parent KPI |
| Year | int | Target year |
| Month | int | Target month (1-12) |
| TargetValue | decimal | Target value for this month |

#### ApplicationUser (extends IdentityUser)
| Field | Type | Description |
|-------|------|-------------|
| FirstName | string | First name |
| LastName | string | Last name |
| FullName | computed | FirstName + LastName |
| DisplayName | string? | Preferred display name |
| Role | string | Denormalized role |
| Department | string | Department |
| AzureObjectId | string? | Azure AD oid claim |
| LoginProvider | string | "Local" or "AzureAD" |
| IsActive | bool | Controls login ability |
| OrgUnitId | Guid? (FK) | Assigned org unit |
| LastLoginAt | DateTime? | Last login timestamp |

#### Document
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| Name | string | Display name |
| FileName | string | Original filename |
| ContentType | string | MIME type |
| FileSize | long | Max 10 MB |
| Content | byte[] | Binary stored in DB |
| RelatedEntityType | string | Polymorphic: "ActionItem", "Project", "Kpi" |
| RelatedEntityId | Guid | Owning entity ID |
| UploadedByUserId | string (FK) | Uploader |

### Permission Entities

#### RolePermission
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| RoleName | string | ASP.NET Identity role name |
| AreaId | Guid | Permission area (denormalized) |
| AreaName | string | Denormalized for fast reads |
| ActionId | Guid | Permission action (denormalized) |
| ActionName | string | Denormalized for fast reads |
| IsActive | bool | Active flag |
| IsDeleted | bool | Soft delete |

#### UserPermissionOverride
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid (PK) | Primary key |
| UserId | string | Application user |
| AreaId/AreaName | Guid/string | Permission area |
| ActionId/ActionName | Guid/string | Permission action |
| IsAllowed | bool | Grant (true) or Deny (false) |
| IsActive | bool | Active flag |
| IsDeleted | bool | Soft delete |

#### AppPermissionArea / AppPermissionAction / AreaPermissionMapping
- Catalog tables defining which areas and actions exist
- Mappings define valid area-action combinations
- Seeded on startup via `PermissionCatalogSeeder`

---

## Services (Application Layer)

| Service Interface | Responsibilities |
|-------------------|------------------|
| `IActionItemService` | CRUD, filtering, stats, comments, overdue processing |
| `IProjectService` | CRUD, strategic objectives lookup, stats, cascade delete |
| `IMilestoneService` | CRUD per project, stats, baselining |
| `IWorkspaceService` | CRUD, summary stats, admin assignment |
| `IDashboardService` | KPI metrics, management dashboard, team workload |
| `IAuthService` | Login, Azure AD validation, token refresh, logout |
| `IUserManagementService` | CRUD users, role assignment, KU employee search, activation |
| `ICommentService` | CRUD polymorphic comments (Project, Milestone) |
| `IDocumentService` | Upload, download, delete documents (polymorphic) |
| `IStrategicObjectiveService` | CRUD by org unit, soft delete/restore |
| `IKpiService` | CRUD, monthly targets, bulk upsert |
| `IOrgUnitService` | Tree, CRUD, soft delete hierarchy |
| `IRoleManagementService` | CRUD roles, user assignment, permission matrix |
| `IPermissionCatalogService` | CRUD areas, actions, mappings |
| `IRolePermissionService` | CRUD role permissions, matrix queries |
| `IUserPermissionOverrideService` | CRUD user overrides |
| `IEffectivePermissionService` | Calculate merged effective permissions |
| `IOrgUnitScopeResolver` | Resolve visible org units for user |

---

## Middleware

| Middleware | Purpose |
|-----------|---------|
| `RequestLoggingMiddleware` | Logs every HTTP request: `[Method] Path responded StatusCode in {N}ms` |
| `ExceptionMiddleware` | Catches unhandled exceptions, returns RFC 7807 ProblemDetails JSON, suppresses stack traces in production |
| `PermissionEnforcement` | Custom middleware applying fine-grained permission checks via PermissionRequirement + PermissionAuthorizationHandler |

---

## Middleware Pipeline Order

1. RequestLoggingMiddleware
2. ExceptionMiddleware
3. HTTPS Redirection (non-dev only)
4. CORS
5. Authentication
6. Authorization
7. PermissionEnforcement
8. Swagger
9. Controllers

---

## Database Seeding (Startup)

1. **Auto-migrate** database on startup
2. **RoleSeeder** - seeds Admin, Manager, User, Viewer roles
3. **PermissionCatalogSeeder** - seeds 12 permission areas, 7 actions, area-action mappings
4. **DefaultRolePermissionsSeeder** - seeds default permissions for each role

---

## Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| Soft Deletes | Preserves audit trail, recoverable via restore endpoints |
| Denormalized Permissions | Stores AreaName/ActionName for fast reads without FK joins |
| Polymorphic Relationships | Documents/Comments use string type discriminators |
| Org Unit Scoping | Multi-tenant data isolation via org unit hierarchy |
| Auto-Seeding | Permission catalog seeded on startup for consistency |
| UTC Everywhere | All datetimes internally UTC with EF value converters |
| Hybrid Auth | Dual JWT schemes (local + Azure AD) routed via tid claim |
| FluentValidation | Auto-validated DTOs before handlers run |
| Async All the Way | All I/O operations use async/await |
| Auto-Generated Codes | ActionId (ACT-001), ProjectCode (PRJ-YYYY-001), MilestoneCode (MS-YYYY-001), ObjectiveCode (SO-001) |
| Progress Clamping | ActionItem.Progress auto-clamped to 0-100 |
| Token Rotation | Refresh tokens invalidated on use for security |
| Cascade Deletes | Soft-deleting projects cascades to milestones and action items |

---

## Configuration (appsettings.json)

| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` | HS256 symmetric signing key |
| `Jwt:Issuer` | JWT issuer |
| `Jwt:Audience` | JWT audience |
| `AzureAd:TenantId` | Microsoft Entra tenant ID |
| `AzureAd:ClientId` | Azure AD application client ID |
| `AllowedOrigins` | CORS allowed origins |
| `Serilog` | Structured logging configuration |

---

## Recent Feature History

1. Browser title set to "KU Action Tracker" with KU logo favicon
2. Default assignee + "Created by Me" section in My Actions
3. Workspace dropdown scoped to user's Level-2 org-unit ancestor
4. My Projects page with role-based access
5. Interactive Gantt chart in project detail
6. Milestone validation blocks project activation when incomplete
7. Dashboard redesign: 6 stat cards per row
8. Excel export and PDF print on all major pages
9. Org unit scoping enforcement across workspaces, projects, action items
10. Permission-based CRUD button visibility
11. Offcanvas drawer for workspace create/edit
12. Full role and permission management system
13. Admin panel: org chart, strategic objectives, KPIs with monthly targets
14. APP_INITIALIZER pre-loads permissions on hard refresh
15. .NET 9 JWT fix: MapInboundClaims for sub -> NameIdentifier
