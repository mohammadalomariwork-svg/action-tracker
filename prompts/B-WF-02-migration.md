# B-WF-02 — EF Core Migration for Workflow Tables

## Context

B-WF-01 added the `ActionItemWorkflowRequest` entity and expanded the `ActionStatus` enum with `Deferred` and `Cancelled`. This prompt creates the EF Core configuration and migration.

## Pre-requisite

- B-WF-01 must be completed and compiling

## Instructions

### 1. Create EF configuration for `ActionItemWorkflowRequest`

**File:** `ActionTracker.Infrastructure/Data/Configurations/ActionItemWorkflowRequestConfiguration.cs` (new)

Create an `IEntityTypeConfiguration<ActionItemWorkflowRequest>` with:

- Table name: `ActionItemWorkflowRequests`
- `Id` as primary key with `HasDefaultValueSql("NEWSEQUENTIALID()")`
- `ActionItemId` as required FK to `ActionItems` table with `DeleteBehavior.Cascade` (when the action item is deleted, its workflow requests go too)
- `RequestType` stored as `int` (enum)
- `Status` stored as `int` (enum) with default `WorkflowRequestStatus.Pending`
- `RequestedByUserId` — required, `nvarchar(450)`, no FK constraint (project convention)
- `RequestedByDisplayName` — required, max 256
- `RequestedNewStartDate`, `RequestedNewDueDate` — optional `datetime2`
- `RequestedNewStatus` — optional `int`
- `CurrentStartDate`, `CurrentDueDate` — optional `datetime2`
- `CurrentStatus` — optional `int`
- `Reason` — required, max 2000
- `ReviewedByUserId` — optional, `nvarchar(450)`, no FK constraint
- `ReviewedByDisplayName` — optional, max 256
- `ReviewComment` — optional, max 2000
- `ReviewedAt` — optional `datetime2`
- `CreatedAt` — required with `HasDefaultValueSql("GETUTCDATE()")`
- `IsDeleted` — required, default `false`
- Index on `ActionItemId` (filtered where `IsDeleted = false`)
- Index on `RequestedByUserId`
- Index on `Status` (for querying pending requests efficiently)

### 2. Register the entity in `ApplicationDbContext`

**File:** `ActionTracker.Infrastructure/Data/ApplicationDbContext.cs` (exists)

Add:
```csharp
public DbSet<ActionItemWorkflowRequest> ActionItemWorkflowRequests { get; set; }
```

Make sure `OnModelCreating` applies configurations from assembly (it already does via `ApplyConfigurationsFromAssembly` — just verify).

### 3. Generate migration

Run from the solution root:

```bash
dotnet ef migrations add AddActionItemWorkflowRequests \
  --project ActionTracker.Infrastructure \
  --startup-project ActionTracker.API
```

### 4. Apply migration

```bash
dotnet ef database update \
  --project ActionTracker.Infrastructure \
  --startup-project ActionTracker.API
```

## Validation

- Migration should create `ActionItemWorkflowRequests` table in SQL Server
- `dotnet build` passes
- Existing data is preserved — this is purely additive
- The `ActionStatus` column on `ActionItems` table remains an `int`; new enum values 5 and 6 are just additional valid integers
