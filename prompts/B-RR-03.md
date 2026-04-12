# B-RR-03 — ProjectRisksController

## Context
B-RR-01 created the entity and migration. B-RR-02 created DTOs, validators, and service. This prompt creates the API controller.

## Requirements

### Create `ProjectRisksController` in `ActionTracker.Api/Controllers/`

Route: `api/projects/{projectId}/risks`

| Method | Route | Permission | Description |
|--------|-------|-----------|-------------|
| GET | `/` | Projects.View | Paginated risks for project. Query params: `page`, `pageSize`, `status`, `rating`, `category` |
| GET | `/{riskId}` | Projects.View | Single risk by ID |
| GET | `/stats` | Projects.View | Risk stats summary for project |
| POST | `/` | Projects.Edit | Create risk. Auto-set `CreatedByUserId` and `CreatedByDisplayName` from JWT claims |
| PUT | `/{riskId}` | Projects.Edit | Update risk |
| DELETE | `/{riskId}` | Projects.Delete | Soft delete risk |
| PATCH | `/{riskId}/restore` | Projects.Edit | Restore soft-deleted risk |

### Controller behavior
- Inject `IProjectRiskService`
- Extract current user ID from `User.FindFirstValue(ClaimTypes.NameIdentifier)`
- Extract display name from `User.FindFirstValue("displayName")` or fall back to email claim
- Return `ApiResponse<T>` wrapper consistent with existing controllers
- Return 404 if risk or project not found
- Use `[Authorize(Policy = "...")]` attributes matching the permission policies from the table above
- Validate `projectId` route param is a valid GUID
- All methods async

## Rules
- Follow the same controller patterns as `MilestonesController` (nested under project route)
- Use existing `ApiResponse<T>` response wrapper
- No new middleware or auth changes needed
