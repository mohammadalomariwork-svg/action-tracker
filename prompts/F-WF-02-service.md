# F-WF-02 — Angular Workflow Service

## Context

The frontend uses Angular services in `src/app/services/` or `src/app/core/services/` to call backend APIs. Services use `HttpClient`, are `@Injectable({ providedIn: 'root' })`, and return `Observable<T>`. The API base URL comes from `environment.ts` or is proxied via `proxy.conf.json`.

Check how existing services like `ActionItemService` construct URLs and follow the same pattern.

## Pre-requisite

- F-WF-01 completed (TypeScript models exist)
- B-WF-06 completed (backend endpoints exist)

## Instructions

### 1. Create `WorkflowService`

**File:** `src/app/services/workflow.service.ts` (new)

```typescript
@Injectable({ providedIn: 'root' })
export class WorkflowService {
  // Base URL should follow the same pattern as other services
  // Likely: private readonly baseUrl = '/api/action-items/workflow';
  // Or using environment: `${environment.apiUrl}/action-items/workflow`
  
  constructor(private http: HttpClient) {}
}
```

### 2. Service Methods

Implement these methods matching the backend controller endpoints:

```typescript
// Date change request
createDateChangeRequest(dto: CreateDateChangeRequest): Observable<WorkflowRequest>

// Status change request  
createStatusChangeRequest(dto: CreateStatusChangeRequest): Observable<WorkflowRequest>

// Review (approve/reject) a request
reviewRequest(requestId: string, dto: ReviewWorkflowRequest): Observable<WorkflowRequest>

// Get pending requests for current user to review
getPendingReviews(page: number = 1, pageSize: number = 20): Observable<PagedResult<WorkflowRequest>>

// Get requests created by current user
getMyRequests(page: number = 1, pageSize: number = 20): Observable<PagedResult<WorkflowRequest>>

// Get all workflow requests for a specific action item
getRequestsForActionItem(actionItemId: string): Observable<WorkflowRequest[]>

// Get pending summary counts (for badge)
getPendingSummary(): Observable<WorkflowRequestSummary>

// Trigger escalation
escalate(actionItemId: string, reason: string): Observable<void>

// Give direction on escalated item
giveDirection(dto: WorkflowDirection): Observable<void>

// Check if current user can review requests for an action item
canReview(actionItemId: string): Observable<CanReviewResponse>
```

### 3. HTTP Method Mapping

| Service Method              | HTTP                                          |
|-----------------------------|-----------------------------------------------|
| `createDateChangeRequest`   | `POST /api/action-items/workflow/date-change-request` |
| `createStatusChangeRequest` | `POST /api/action-items/workflow/status-change-request` |
| `reviewRequest`             | `PUT /api/action-items/workflow/requests/{requestId}/review` |
| `getPendingReviews`         | `GET /api/action-items/workflow/pending-reviews?page=&pageSize=` |
| `getMyRequests`             | `GET /api/action-items/workflow/my-requests?page=&pageSize=` |
| `getRequestsForActionItem`  | `GET /api/action-items/workflow/action-item/{actionItemId}` |
| `getPendingSummary`         | `GET /api/action-items/workflow/pending-summary` |
| `escalate`                  | `POST /api/action-items/workflow/escalate` |
| `giveDirection`             | `POST /api/action-items/workflow/give-direction` |
| `canReview`                 | `GET /api/action-items/workflow/can-review/{actionItemId}` |

### 4. Use `PagedResult<T>`

Use the existing `PagedResult<T>` interface that's already defined in the project's models. If it wraps the response differently (e.g., `ApiResponse<PagedResult<T>>`), match that pattern.

### 5. Proxy config

**File:** `proxy.conf.json` (exists)

Verify that the proxy already covers `/api/*` routes. If it does (which it should since all API calls go through `/api/`), no changes needed. If the proxy uses specific path patterns, add:
```json
"/api/action-items/workflow": {
  "target": "https://localhost:7135",
  "secure": false
}
```

## Validation

- `ng build` passes
- Service is injectable
- All methods are properly typed
- URLs match the backend controller routes
