# F-PW-01 — TypeScript Models, Enums, and ProjectWorkflowService

## Context

The backend now exposes project workflow endpoints at `/api/projects/workflow`. This prompt creates the frontend TypeScript models and the Angular service to communicate with those endpoints.

## What to do

### 1. Add `PendingApproval` to the existing `ProjectStatus` enum

Find the existing `ProjectStatus` enum in `src/app/models/` (or `src/app/core/models/`). Add `PendingApproval = 'PendingApproval'` as a new value. This matches the backend enum addition from B-PW-02.

### 2. Create `ProjectApprovalStatus` enum

In the same models folder, add:
```typescript
export enum ProjectApprovalStatus {
  Pending = 'Pending',
  Approved = 'Approved',
  Rejected = 'Rejected'
}
```

### 3. Create `ProjectApprovalRequest` interface

In the models folder, add:
```typescript
export interface ProjectApprovalRequest {
  id: string;
  projectId: string;
  projectCode: string;
  projectName: string;
  requestedByUserId: string;
  requestedByDisplayName: string;
  reviewedByUserId: string | null;
  reviewedByDisplayName: string | null;
  status: ProjectApprovalStatus;
  reason: string;
  reviewComment: string | null;
  createdAt: string;
  reviewedAt: string | null;
}
```

### 4. Create `ProjectApprovalSummary` interface

```typescript
export interface ProjectApprovalSummary {
  pendingProjectApprovals: number;
}
```

### 5. Create request interfaces

```typescript
export interface SubmitProjectApprovalRequest {
  projectId: string;
  reason: string;
}

export interface ReviewProjectApprovalRequest {
  requestId: string;
  isApproved: boolean;
  reviewComment: string | null;
}
```

### 6. Create `ProjectWorkflowService` in `src/app/services/`

The service should follow the same pattern as the existing `WorkflowService` (for action items). Inject `HttpClient` and use the API base URL from the environment config.

**Base URL:** `/api/projects/workflow`

Methods:

| Method | HTTP | Endpoint | Returns |
|--------|------|----------|---------|
| `submitForApproval(dto)` | `POST` | `/submit` | `Observable<ProjectApprovalRequest>` |
| `reviewApprovalRequest(requestId, dto)` | `PUT` | `/requests/{requestId}/review` | `Observable<ProjectApprovalRequest>` |
| `getApprovalRequestsForProject(projectId)` | `GET` | `/project/{projectId}` | `Observable<ProjectApprovalRequest[]>` |
| `getPendingReviews()` | `GET` | `/pending-reviews` | `Observable<ProjectApprovalRequest[]>` |
| `getMyRequests()` | `GET` | `/my-requests` | `Observable<ProjectApprovalRequest[]>` |
| `getPendingSummary()` | `GET` | `/pending-summary` | `Observable<ProjectApprovalSummary>` |
| `canReviewProject(projectId)` | `GET` | `/can-review/{projectId}` | `Observable<{ canReview: boolean }>` |

### 7. Update `WorkflowStateService` to include project approval counts

The existing `WorkflowStateService` maintains a `BehaviorSubject` for the pending approval count shown in the header badge. Extend it:
- Add a property `pendingProjectApprovals$` (or merge the count into the existing total)
- In the `refreshPendingCount()` method, also call `ProjectWorkflowService.getPendingSummary()` and add `pendingProjectApprovals` to the total pending count
- The header badge should show the combined total (action item workflow + project workflow pending counts)

## Files to create
- `src/app/models/project-approval.model.ts` (or add to the existing project model file)
- `src/app/services/project-workflow.service.ts`

## Files to modify
- Existing `ProjectStatus` enum file — add `PendingApproval`
- `WorkflowStateService` — include project approval counts in the pending total

## Do NOT
- Do not create components in this step
- Do not modify routing
- Do not modify the existing `WorkflowService` (action item workflow)
