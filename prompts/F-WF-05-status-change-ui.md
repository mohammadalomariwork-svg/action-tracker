# F-WF-05 — Workflow UI in Action Detail View

## Context

The existing `ActionDetailComponent` shows the full details of an action item with comments and documents sections. We need to add workflow-specific UI here: pending request alerts, escalation direction panel, and workflow request history.

## Pre-requisite

- F-WF-02 completed (WorkflowService exists)
- F-WF-04 completed (action form has workflow integration)

## Instructions

### 1. Modify `ActionDetailComponent`

**File:** Find the existing action detail component (likely `src/app/features/actions/action-detail/action-detail.component.ts`)

#### Add dependencies:
- Inject `WorkflowService`
- Inject `AuthService` (to get current user ID)

#### New state:
- `workflowRequests: WorkflowRequest[]`
- `pendingRequests: WorkflowRequest[]` (filtered from workflowRequests)
- `canReview: boolean`
- `isCreator: boolean` (current user is the action item creator)
- `isEscalated: boolean` (from the action item)
- `showDirectionForm: boolean`

#### On init (after loading the action item):
1. Load workflow requests: `workflowService.getRequestsForActionItem(actionItemId)`
2. Filter pending: `pendingRequests = workflowRequests.filter(r => r.status === 'Pending')`
3. Check review capability: `workflowService.canReview(actionItemId)`
4. Check if current user is creator: `isCreator = actionItem.createdByUserId === currentUserId`

### 2. Template Additions

#### A. Pending Requests Alert (top of detail view)

If `pendingRequests.length > 0`, show an alert panel:

```
┌─────────────────────────────────────────────────────────────┐
│ ⏳ Pending Workflow Requests                                │
│                                                             │
│ ┌─ Date Change Request ──────────────────────────────────┐  │
│ │ Requested by: John Doe • 2 hours ago                   │  │
│ │ Current: 2026-04-01 → 2026-04-30                       │  │
│ │ Proposed: 2026-04-05 → 2026-05-15                      │  │
│ │ Reason: "Need more time due to dependency delays"       │  │
│ │                                                         │  │
│ │ [✅ Approve]  [❌ Reject]     (if canReview)            │  │
│ └─────────────────────────────────────────────────────────┘  │
│                                                             │
│ ┌─ Status Change Request ────────────────────────────────┐  │
│ │ Requested by: Jane Smith • 1 day ago                   │  │
│ │ Current Status: In Progress → Completed                │  │
│ │ Reason: "All deliverables have been submitted"          │  │
│ │                                                         │  │
│ │ [✅ Approve]  [❌ Reject]     (if canReview)            │  │
│ └─────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

- Each pending request is a card with request details
- Approve/Reject buttons only show if `canReview` is true
- Clicking Approve: show confirmation with optional comment
- Clicking Reject: show dialog with required comment
- After review: refresh the requests list, show toast

#### B. Escalation Direction Panel

If `actionItem.isEscalated && (isCreator || canReview)`:

Show a section below the action item details:

```
┌─────────────────────────────────────────────────────────────┐
│ ⚠️ This action item has been escalated                      │
│                                                             │
│ As the [creator / direct manager], you can provide          │
│ direction to the assignee.                                  │
│                                                             │
│ [📝 Give Direction]                                         │
└─────────────────────────────────────────────────────────────┘
```

When "Give Direction" is clicked:
- Show an inline textarea for the direction text
- On submit: call `workflowService.giveDirection({ actionItemId, directionText })`
- Show toast: "Direction sent. The assignee has been notified."
- The direction is saved as a high-importance comment and will appear in the comments section

#### C. Workflow Request History Section

Add a new section after comments (or as a tab alongside comments/documents):

**Title:** "Workflow History"

Show all workflow requests as a timeline or card list:
- Each entry shows: type badge, status badge, requester, dates/status change details, reason
- If reviewed: show reviewer, review comment, reviewed date
- Most recent first
- Use a subtle gray background to distinguish from comments

### 3. Styling

- Pending requests alert: use a slightly elevated card with a left border accent (amber for pending)
- Escalation panel: amber/warning background with alert styling
- Workflow history: timeline-style with status indicators
- All responsive — cards stack on mobile
- Match existing component styling patterns

### 4. Review Dialog Component

Create a reusable review dialog component that can be used both here and in the My Approvals page:

**File:** `src/app/shared/components/workflow-review-dialog/workflow-review-dialog.component.ts` (new)

Props (inputs):
- `request: WorkflowRequest` — the request being reviewed
- `visible: boolean` — show/hide

Events (outputs):
- `reviewed: EventEmitter<{ requestId: string, isApproved: boolean, reviewComment: string | null }>`
- `closed: EventEmitter<void>`

Template:
- Modal or offcanvas dialog
- Shows request details summary
- Comment textarea (required for rejection)
- Approve (green) and Reject (red) buttons
- Loading state while submitting

### 5. Integration Notes

- The review dialog component should be **standalone** and imported in both `MyApprovalsComponent` and `ActionDetailComponent`
- Use the same toast service for success/error messages
- After any review action, also call `getPendingSummary()` to update badge counts (for the notification bell or nav badge)

## Validation

- Action detail shows pending requests when they exist
- Approve/Reject works from the detail view
- Escalation direction panel appears for escalated items
- Direction submission creates a high-importance comment
- Workflow history shows all past requests
- Review dialog is reusable across components
- `ng build` passes
