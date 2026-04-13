# B-PW-08 / F-PW-06 — Documentation Updates

## Context

After all prompts B-PW-01 through B-PW-07 and F-PW-01 through F-PW-05 have been executed successfully, update the project documentation files to reflect the new feature.

## What to do

### 1. Update `status.md`

Under section **4. Projects**, add a new subsection:

```
### Project Approval Workflow
- Project creation notifications sent to direct line manager and all sponsors (email + in-app)
- Submit for approval workflow: Draft → PendingApproval → Active
- Sponsor or direct line manager can approve or reject
- On approval: project activated, dates baselined, all milestones baselined
- On rejection: project returned to Draft with reviewer comment
- Date freeze enforcement: project, milestone, and action item dates locked after submission
- Approval history visible on project detail page
- Project approvals tab added to My Approvals page
- Pending project approval count included in header badge
```

Under section **15. Action Item Workflow** (or create a new section **17. Project Workflow** if it makes more sense), add appropriate entries.

Under **16. Not Yet Implemented**, remove any related items if they were listed.

### 2. Update `BACKEND_OVERVIEW.md`

#### API Controllers section
Add a new entry for `ProjectWorkflowController`:

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/projects/workflow/submit` | Submit project for start-approval |
| PUT | `/api/projects/workflow/requests/{requestId}/review` | Approve or reject |
| GET | `/api/projects/workflow/project/{projectId}` | Approval requests for project |
| GET | `/api/projects/workflow/pending-reviews` | Pending reviews for current user |
| GET | `/api/projects/workflow/my-requests` | Requests submitted by current user |
| GET | `/api/projects/workflow/pending-summary` | Pending count for badge |
| GET | `/api/projects/workflow/can-review/{projectId}` | Check review eligibility |

#### Database Entities section
Add `ProjectApprovalRequest` entity table.

#### Enums section
- Add `ProjectApprovalStatus: Pending, Approved, Rejected`
- Update `ProjectStatus` to include `PendingApproval`

#### Services section
Add `IProjectWorkflowService` with its responsibilities.

### 3. Update `FRONTEND_OVERVIEW.md`

#### Routing section
No new routes (all integrated into existing pages).

#### Services section
Add `ProjectWorkflowService` with its endpoints.

#### Models section
Add `ProjectApprovalRequest`, `ProjectApprovalStatus`, `ProjectApprovalSummary`, `SubmitProjectApprovalRequest`, `ReviewProjectApprovalRequest`.

#### Components section
Note updates to `MyApprovalsComponent` (third tab), `ProjectDetailComponent` (approval banner + history), `ProjectFormComponent` (date freeze), `MyProjectsComponent` (create button).

#### Key Features → Projects section
Add bullet points about the approval workflow and date freeze.

### 4. Update `CLAUDE.MD`

Under **Implemented Features → Core Functionality**, add:
```
- [x] Project approval workflow (submit for approval, sponsor/manager review, date freeze)
- [x] Project creation notifications to sponsors and direct line manager
```

Update the entity list if needed.

## Files to modify
- `status.md`
- `BACKEND_OVERVIEW.md`
- `FRONTEND_OVERVIEW.md`
- `CLAUDE.MD`

## Do NOT
- Do not document features that were not implemented
- Do not change formatting conventions in the documentation files
- Update the `Last updated:` date in each file
