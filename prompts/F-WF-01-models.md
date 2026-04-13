# F-WF-01 — TypeScript Models for Action Item Workflow

## Context

The Angular frontend uses TypeScript interfaces in `src/app/models/` (or `src/app/core/models/`). The existing `ActionItem` interface, `ActionStatus` enum, and related types are already defined there. We need to add workflow-related models.

## Pre-requisite

- B-WF-04 completed (backend DTOs define the contract)

## Instructions

### 1. Update `ActionStatus` enum

**File:** Find the existing `ActionStatus` enum (likely in `src/app/models/action-item.model.ts` or similar)

Add the two new values:
```typescript
Deferred = 5,
Cancelled = 6
```

Also update any status label/color maps that exist. Look for objects like `STATUS_LABELS`, `STATUS_COLORS`, `STATUS_BADGE_CLASSES`, etc. and add:
- `Deferred`: label "Deferred", color — use a muted purple or gray badge (check the project's badge color pattern)
- `Cancelled`: label "Cancelled", color — use a dark gray or strikethrough style badge

### 2. Create workflow models file

**File:** `src/app/models/workflow.model.ts` (new)

```typescript
export enum WorkflowRequestType {
  DateChangeRequest = 0,
  StatusChangeRequest = 1
}

export enum WorkflowRequestStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2
}

export interface WorkflowRequest {
  id: string;
  actionItemId: string;
  actionItemCode: string;
  actionItemTitle: string;
  requestType: string;      // 'DateChangeRequest' | 'StatusChangeRequest'
  status: string;            // 'Pending' | 'Approved' | 'Rejected'
  requestedByUserId: string;
  requestedByDisplayName: string;
  requestedNewStartDate: string | null;
  requestedNewDueDate: string | null;
  requestedNewStatus: string | null;
  currentStartDate: string | null;
  currentDueDate: string | null;
  currentStatus: string | null;
  reason: string;
  reviewedByUserId: string | null;
  reviewedByDisplayName: string | null;
  reviewComment: string | null;
  reviewedAt: string | null;
  createdAt: string;
}

export interface WorkflowRequestSummary {
  pendingDateChanges: number;
  pendingStatusChanges: number;
  totalPending: number;
}

export interface CreateDateChangeRequest {
  actionItemId: string;
  newStartDate: string | null;
  newDueDate: string | null;
  reason: string;
}

export interface CreateStatusChangeRequest {
  actionItemId: string;
  newStatus: number;   // ActionStatus enum value
  reason: string;
}

export interface ReviewWorkflowRequest {
  isApproved: boolean;
  reviewComment: string | null;
}

export interface WorkflowDirection {
  actionItemId: string;
  directionText: string;
}

export interface CanReviewResponse {
  canReview: boolean;
}
```

### 3. Add workflow request status labels and colors

In the same file or in a shared constants file:

```typescript
export const WORKFLOW_STATUS_CONFIG: Record<string, { label: string; cssClass: string }> = {
  Pending:  { label: 'Pending',  cssClass: 'bg-warning text-dark' },
  Approved: { label: 'Approved', cssClass: 'bg-success text-white' },
  Rejected: { label: 'Rejected', cssClass: 'bg-danger text-white' }
};

export const WORKFLOW_TYPE_LABELS: Record<string, string> = {
  DateChangeRequest: 'Date Change',
  StatusChangeRequest: 'Status Change'
};
```

### 4. Export from barrel file

If the project uses barrel exports (`index.ts` files in the models folder), add the new models to the barrel.

## Validation

- `ng build` passes (or `ng serve` compiles without errors)
- All existing TypeScript type references to `ActionStatus` still work
- New models are importable from the models path
