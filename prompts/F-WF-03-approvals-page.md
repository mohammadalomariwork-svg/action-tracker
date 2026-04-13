# F-WF-03 — My Approvals Page

## Context

This is a new page where users see all pending workflow requests that require their review (they are either the action item creator or the direct manager of an assignee). They can approve or reject requests from this page.

Follow the same design patterns as "My Actions" page: stat cards at top, filterable/sortable list below, responsive card layout on mobile. Use Bootstrap 5, SCSS variables, and the project's existing design language (sapphire blue accent `#0F52BA`, white surfaces, Plus Jakarta Sans font).

## Pre-requisite

- F-WF-02 completed (WorkflowService exists)

## Instructions

### 1. Create the component

**File:** `src/app/features/workflow/my-approvals/my-approvals.component.ts` (new)
**File:** `src/app/features/workflow/my-approvals/my-approvals.component.html` (new)
**File:** `src/app/features/workflow/my-approvals/my-approvals.component.scss` (new)

This must be a **standalone component** (no NgModule). Follow the same component creation pattern as existing feature components.

### 2. Component Logic

**On init:**
1. Call `workflowService.getPendingSummary()` to populate stat cards
2. Call `workflowService.getPendingReviews(page, pageSize)` to load the list
3. Also load `workflowService.getMyRequests(page, pageSize)` for a "My Requests" tab

**State:**
- `activeTab`: `'pending-reviews'` | `'my-requests'` (default: `'pending-reviews'`)
- `pendingReviews`: `WorkflowRequest[]`
- `myRequests`: `WorkflowRequest[]`
- `summary`: `WorkflowRequestSummary`
- `page`, `pageSize`, `totalCount` for pagination
- `isLoading`: boolean
- `selectedRequest`: `WorkflowRequest | null` (for review modal)

### 3. Template Structure

#### Header
- Page title: "My Approvals" with breadcrumb
- Use the project's `PageHeaderComponent` if it exists

#### Stat Cards Row (3 cards)
- **Pending Date Changes** — `summary.pendingDateChanges` — amber/warning color
- **Pending Status Changes** — `summary.pendingStatusChanges` — blue/info color
- **Total Pending** — `summary.totalPending` — primary color

Use the same card styling pattern as the stat cards on the "My Actions" page.

#### Tabs
- **Pending Reviews** — requests waiting for this user's decision
- **My Requests** — requests this user has submitted (shows their status: Pending/Approved/Rejected)

#### Request Cards/Table

For each request, show:
- **Action Item Code & Title** — clickable link to the action item detail page
- **Request Type** badge — "Date Change" or "Status Change" (use `WORKFLOW_TYPE_LABELS`)
- **Status** badge — Pending/Approved/Rejected (use `WORKFLOW_STATUS_CONFIG`)
- **Requester** — display name
- **Submitted** — relative time (e.g., "2 hours ago") using date-fns `formatDistanceToNow`
- **Details:**
  - For date change: show current dates → proposed dates
  - For status change: show current status → proposed status
- **Reason** — the requester's justification
- **Action buttons** (only on "Pending Reviews" tab):
  - ✅ **Approve** button (green)
  - ❌ **Reject** button (red)

#### Review Modal/Dialog

When clicking Approve or Reject, show a confirmation dialog (use the existing `ConfirmDialogComponent` pattern or create an offcanvas/modal):
- For **Approve**: Optional comment field, confirm button
- For **Reject**: Required comment field (explain why), reject button

After submitting the review:
1. Call `workflowService.reviewRequest(requestId, dto)`
2. Show toast notification (success/error)
3. Remove the request from the pending list
4. Update the summary counts

#### "My Requests" Tab

Similar card layout but:
- No approve/reject buttons
- Show the review status, reviewer name, review comment, reviewed date
- Pending items show a "Waiting for review" label
- Approved items show ✅ and the reviewer info
- Rejected items show ❌ and the rejection reason

### 4. Styling

- Use SCSS variables from the project's global styles
- Cards should have subtle shadows, rounded corners
- Responsive: on mobile, cards stack vertically
- Status badges: use the existing `StatusBadgeComponent` pattern or Bootstrap badge classes
- Dates displayed in a clear format (check how existing components format dates)
- Empty state: show a friendly message when there are no pending reviews ("All caught up! No pending approvals.")

### 5. Pagination

Use the same pagination pattern as the existing action list:
- Page size selector (10, 20, 50)
- Previous/Next buttons
- Page count display

## Validation

- Component compiles without errors
- Displays pending reviews and my requests in two tabs
- Approve/Reject buttons work and update the list
- Responsive on mobile
- Follows existing design patterns and SCSS variables
- Uses standalone component pattern (no NgModule)
