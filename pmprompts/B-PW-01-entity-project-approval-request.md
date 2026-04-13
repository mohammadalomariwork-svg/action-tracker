# B-PW-01 — Entity: ProjectApprovalRequest + Migration

## Context

The KU Action Tracker already has a workflow request pattern for action items (`ActionItemWorkflowRequest` entity in the Domain layer). We now need a similar entity for **project start-approval requests**. The project manager submits a request; sponsors and the direct line manager can approve or reject.

## What to do

### 1. Create a new entity `ProjectApprovalRequest` in `ActionTracker.Domain/Entities/`

Fields:

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` (PK) | Primary key, default `NEWSEQUENTIALID()` |
| `ProjectId` | `Guid` (FK → Project) | The project being submitted for approval |
| `RequestedByUserId` | `string` | User ID of the person who submitted the request |
| `RequestedByDisplayName` | `string` | Denormalized display name of requester |
| `ReviewedByUserId` | `string?` | User ID of the reviewer (null while pending) |
| `ReviewedByDisplayName` | `string?` | Denormalized display name of reviewer |
| `Status` | `ProjectApprovalStatus` enum | Pending, Approved, Rejected |
| `Reason` | `string` | Justification provided by the requester |
| `ReviewComment` | `string?` | Comment from the reviewer on approve/reject |
| `CreatedAt` | `DateTime` | UTC timestamp of request creation |
| `ReviewedAt` | `DateTime?` | UTC timestamp of review |
| `IsDeleted` | `bool` | Soft delete flag, default `false` |

Navigation properties:
- `public Project Project { get; set; }` (navigation to Project)

### 2. Create enum `ProjectApprovalStatus` in `ActionTracker.Domain/Enums/`

Values: `Pending = 0`, `Approved = 1`, `Rejected = 2`

### 3. Add `PendingApproval` to existing `ProjectStatus` enum

The existing `ProjectStatus` enum has: `Draft = 0, Active = 1, Suspended = 2, Closed = 3`. Add `PendingApproval = 4` after `Closed`. This represents the state between Draft and Active where the project is awaiting sponsor/manager approval.

### 4. Add navigation property on `Project` entity

Add to `Project.cs`:
```
public ICollection<ProjectApprovalRequest> ApprovalRequests { get; set; } = new List<ProjectApprovalRequest>();
```

### 5. Register in `ApplicationDbContext`

- Add `DbSet<ProjectApprovalRequest> ProjectApprovalRequests { get; set; }`
- Configure the entity in `OnModelCreating`:
  - Table name: `ProjectApprovalRequests`
  - `Id` has default value `NEWSEQUENTIALID()`
  - `ProjectId` FK to `Projects` with `DeleteBehavior.Cascade`
  - `Status` stored as `int`
  - `Reason` max length 2000
  - `ReviewComment` max length 2000
  - `RequestedByUserId` and `ReviewedByUserId` are plain strings (no FK to AspNetUsers — matches existing project convention)
  - Soft delete query filter: `.HasQueryFilter(e => !e.IsDeleted)`
  - Index on `ProjectId`
  - Index on `RequestedByUserId`
  - Index on `Status`

### 6. Create EF Core migration

Run the migration with the name `AddProjectApprovalRequest`. Use the correct `--project` and `--startup-project` flags per the project's conventions:

```
dotnet ef migrations add AddProjectApprovalRequest --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

## Files to create
- `ActionTracker.Domain/Enums/ProjectApprovalStatus.cs`
- `ActionTracker.Domain/Entities/ProjectApprovalRequest.cs`
- New migration files (auto-generated)

## Files to modify
- `ActionTracker.Domain/Enums/ProjectStatus.cs` — add `PendingApproval = 4`
- `ActionTracker.Domain/Entities/Project.cs` — add `ApprovalRequests` navigation property
- `ActionTracker.Infrastructure/Data/ApplicationDbContext.cs` — add DbSet + entity configuration

## Do NOT
- Do not modify any controllers or services in this step
- Do not create DTOs yet
- Do not change any existing migration files
