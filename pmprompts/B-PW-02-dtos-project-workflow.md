# B-PW-02 — DTOs for Project Approval Workflow

## Context

The `ProjectApprovalRequest` entity and `ProjectApprovalStatus` enum were created in B-PW-01. Now create the DTOs that the API will use for the project workflow feature. All DTOs go in the Application layer following existing conventions.

## What to do

### 1. Create DTOs in `ActionTracker.Application/DTOs/Projects/`

#### `SubmitProjectApprovalRequestDto.cs`
Request body for submitting a project for approval.

| Field | Type | Validation |
|-------|------|------------|
| `ProjectId` | `Guid` | Required |
| `Reason` | `string` | Required, max 2000 characters |

#### `ReviewProjectApprovalRequestDto.cs`
Request body for approving or rejecting a project approval request.

| Field | Type | Validation |
|-------|------|------------|
| `RequestId` | `Guid` | Required |
| `IsApproved` | `bool` | Required |
| `ReviewComment` | `string?` | Required when `IsApproved` is `false`, max 2000 characters |

#### `ProjectApprovalRequestDto.cs`
Response DTO returned to the frontend.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Request ID |
| `ProjectId` | `Guid` | Project ID |
| `ProjectCode` | `string` | Project code (e.g. PRJ-2026-001) |
| `ProjectName` | `string` | Project name |
| `RequestedByUserId` | `string` | Requester user ID |
| `RequestedByDisplayName` | `string` | Requester display name |
| `ReviewedByUserId` | `string?` | Reviewer user ID |
| `ReviewedByDisplayName` | `string?` | Reviewer display name |
| `Status` | `string` | "Pending", "Approved", or "Rejected" |
| `Reason` | `string` | Requester's justification |
| `ReviewComment` | `string?` | Reviewer's comment |
| `CreatedAt` | `DateTime` | Request creation timestamp |
| `ReviewedAt` | `DateTime?` | Review timestamp |

#### `ProjectApprovalSummaryDto.cs`
Summary counts for the header badge and My Approvals page.

| Field | Type | Description |
|-------|------|-------------|
| `PendingProjectApprovals` | `int` | Count of pending project approval requests where the current user is a reviewer |

### 2. Create FluentValidation validators in `ActionTracker.Application/Validators/Projects/`

#### `SubmitProjectApprovalRequestValidator.cs`
- `ProjectId` must not be empty
- `Reason` must not be empty and max length 2000

#### `ReviewProjectApprovalRequestValidator.cs`
- `RequestId` must not be empty
- When `IsApproved` is `false`, `ReviewComment` must not be empty
- `ReviewComment` max length 2000

## Files to create
- `ActionTracker.Application/DTOs/Projects/SubmitProjectApprovalRequestDto.cs`
- `ActionTracker.Application/DTOs/Projects/ReviewProjectApprovalRequestDto.cs`
- `ActionTracker.Application/DTOs/Projects/ProjectApprovalRequestDto.cs`
- `ActionTracker.Application/DTOs/Projects/ProjectApprovalSummaryDto.cs`
- `ActionTracker.Application/Validators/Projects/SubmitProjectApprovalRequestValidator.cs`
- `ActionTracker.Application/Validators/Projects/ReviewProjectApprovalRequestValidator.cs`

## Files to modify
- None

## Do NOT
- Do not create services or controllers yet
- Do not modify existing DTOs
