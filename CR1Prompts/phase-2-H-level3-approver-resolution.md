# Prompt: CR-001-H — Level-3 Approver Resolution

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-H
> **Phase:** 2 (Approval gates)
> **Depends on:** Phase 1 complete
> **Blocks:** CR-001-D (the approval workflow needs to know who the approver is)

---

## 0. Read first

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — `OrgUnit` entity, `IOrgUnitScopeResolver`,
   `IStrategicScopeService`
3. `backend/ActionTracker/ActionTracker.Domain/Entities/OrgUnit.cs`
4. `backend/ActionTracker/ActionTracker.Application/UserManagement/` — the
   role assignment validation that already enforces "Strategy Editor must
   be at level ≥ 2"
5. `backend/ActionTracker/ActionTracker.Infrastructure/Services/StrategicScopeService.cs`

---

## 1. Open questions — defaults applied here

| # | Default applied |
|---|---|
| Q3 | Add `OrgUnit.HeadUserId` (nullable FK). Keep this lightweight — no new role. |
| Q9 | Level 1 = root, level 3 = a deeper ancestor. The reviewer for a Strategy Editor scoped at level ≥ 2 is the head of the **closest level-3 ancestor** of the editor's org unit (i.e. walk up until `Level == 3`, take that node's `HeadUserId`). |
| Q11 | PMO Head is **not** a fallback approver. If no level-3 ancestor head exists, surface a clear error to the submitter rather than silently routing elsewhere. |

If the requestor's interpretation of "level 3" differs (e.g. level 1 = leaf
in their tree), stop and update this prompt — the resolver logic flips.

---

## 2. Scope

This prompt does **not** add the approval workflow. It only adds:

1. The `OrgUnit.HeadUserId` field.
2. A new service `IApprovalAuthorityResolver` that resolves "who is the
   plan approver for org unit X?".
3. Tightened validation on the Strategy Editor role assignment.
4. Admin UI to assign / unassign an org-unit head.

CR-001-D will consume this resolver to populate the approver on each
`StrategicPlanApprovalRequest`.

---

## 3. Domain layer

### 3.1 Modify `Entities/OrgUnit.cs`

Add:

| Field | Type | Notes |
|---|---|---|
| `HeadUserId` | `Guid?` FK → `ApplicationUser` | Nullable. Org units below level 3 typically won't have one. |
| `HeadUser` | nav | |

### 3.2 Permissions

Add `OrgUnits.AssignHead` to the permission area. Default grants:

- **Admin:** ✓
- **PMO Head:** ✓ (so PMO can keep heads up to date as a governance task)
- All others: –

---

## 4. Application layer

### 4.1 New service `IApprovalAuthorityResolver`

`Application/Common/Interfaces/IApprovalAuthorityResolver.cs`:

```csharp
public interface IApprovalAuthorityResolver
{
    /// <summary>
    /// Resolves the user who must approve strategic plans / KPI actuals
    /// authored within the given org unit. Returns null if no approver
    /// can be resolved (caller must surface a clear error).
    /// </summary>
    Task<ApprovalAuthorityDto?> ResolvePlanApproverAsync(
        Guid orgUnitId,
        CancellationToken ct = default);
}

public record ApprovalAuthorityDto(
    Guid UserId,
    string DisplayName,
    string Email,
    Guid SourceOrgUnitId,   // the level-3 ancestor we resolved to
    int SourceOrgUnitLevel);
```

### 4.2 Resolver behaviour

Implementation in `Infrastructure/Services/ApprovalAuthorityResolver.cs`:

1. Load the target `OrgUnit` (must exist and not be soft-deleted).
2. Walk up the parent chain until `Level == 3` (root = level 1) **or** the
   chain ends.
3. If a level-3 ancestor is found and it has a non-null `HeadUserId`
   pointing at an active user → return that user.
4. If the target org unit itself is at level < 3 (e.g. someone is editing
   for a level-2 unit), still walk up to level 3 — the editor's plan is
   approved at that level by design.
5. If no level-3 ancestor has a head, return `null`. Do **not** fall back
   to any other authority (per Q11 default).
6. Cache the result per org unit for the request lifetime (use the existing
   scoped-cache pattern if there is one, else just don't bother — this is
   not hot).

### 4.3 New CQRS

| Command/Query | Notes |
|---|---|
| `AssignOrgUnitHeadCommand(orgUnitId, userId)` | Validates: org unit must be level ≥ 3 (warn but allow if level < 3 — admins might want it for testing); user must be active. Permission: `OrgUnits.AssignHead`. |
| `RemoveOrgUnitHeadCommand(orgUnitId)` | Sets `HeadUserId = null`. Same permission. |
| `GetOrgUnitHeadQuery(orgUnitId)` | Returns the head user DTO or null. |

Add validators that block assigning a deactivated user as head.

### 4.4 Strategy Editor assignment validation (tightening)

`UserManagementService.UpdateRoleAsync` (or wherever the Strategy Editor
assignment validation lives — see `status.md` §11) currently rejects
assigning Strategy Editor to users below level 2. **Extend** that check:

After the level-2 check, also call `ResolvePlanApproverAsync` against the
target user's `OrgUnitId`. If it returns null, **warn** (not reject) the
admin — they can still assign the role, but make the warning explicit:

```
"User assigned Strategy Editor, but no plan approver is currently configured
 for the level-3 ancestor of their org unit. Plans this user submits cannot
 be approved until an org-unit head is assigned."
```

Surface this warning through the existing API response shape — likely a
`Warnings` array in the success response. If no such shape exists, add one
or pick the lightest-touch alternative used elsewhere.

---

## 5. Infrastructure layer

### 5.1 EF configuration

Update `OrgUnitConfiguration` to add the FK with `DeleteBehavior.SetNull`
(if the head user is deleted, the FK clears — preferable to blocking
user deletion).

### 5.2 Migration

```bash
dotnet ef migrations add Add_OrgUnit_HeadUserId --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

No data backfill — heads will be assigned by Admin / PMO Head through the UI
during rollout.

### 5.3 Resolver registration

Register `IApprovalAuthorityResolver` as scoped in DI (match the lifetime of
`IStrategicScopeService`).

---

## 6. API layer

Extend `OrgUnitsController` (or create one if it doesn't exist):

| Method | Endpoint | Permission |
|---|---|---|
| GET | `/api/org-units/{id}/head` | `OrgUnits.View` |
| PUT | `/api/org-units/{id}/head` | `OrgUnits.AssignHead` |
| DELETE | `/api/org-units/{id}/head` | `OrgUnits.AssignHead` |
| GET | `/api/org-units/{id}/plan-approver` | `StrategicPlans.View` (returns the resolver result, useful for the frontend to preview who will receive a future approval request) |

---

## 7. Frontend

### 7.1 Org chart admin UI

The org-chart admin page (per `FRONTEND_OVERVIEW.md`) needs an "Assign head"
action per node. UX:

- A button or context-menu item "Assign head…" — gated by
  `*hasPermission="'OrgUnits.AssignHead'"`.
- Opens a user picker (re-use the workspace-admin user picker from
  `status.md` §3 — searches name, email, org unit; chip display).
- On save, call `PUT /api/org-units/{id}/head`.
- Show the current head as a small badge under the org unit name.
- Allow clearing the head with a small "x" on the badge.

### 7.2 Strategy page approver preview

On the Strategy page (CR-001-B), add a small read-only line under the plan
header card: "Approver: {DisplayName} (head of {OrgUnitName})", or
"Approver: not yet configured" with a link to the org-chart admin (gated by
permission).

This sets expectations before CR-001-D ships the actual submit button.

---

## 8. Tests

- `ApprovalAuthorityResolverTests`:
  - Walks up to level 3 correctly.
  - Returns null when no level-3 ancestor has a head.
  - Returns null when the head user is deactivated.
  - Returns null when the target org unit is soft-deleted.
- Strategy Editor assignment test: the warning is surfaced when no approver
  is resolvable.
- Frontend test: the "Assign head" button is hidden for users without the
  permission.

---

## 9. Acceptance criteria

1. Migration applies cleanly.
2. Admin can assign and remove an org-unit head through the org-chart UI.
3. `GET /api/org-units/{id}/plan-approver` returns the correct user for
   units at level 2+ whose level-3 ancestor has a head.
4. Strategy Editor role assignment surfaces the "no approver configured"
   warning when applicable, but does not reject the assignment.
5. Soft-deleting a user that is currently an org-unit head leaves the
   `HeadUserId` cleared (not pointing at a tombstone).

---

## 10. Documentation updates

- `status.md` — under section 11 (User Management), update the Strategy
  Editor validation note to mention the level-3-approver warning. Add a new
  bullet under section 2 (Organizational Structure): "Org units may have a
  designated head user; heads at level 3 act as the strategic-plan
  approver for their subtree." Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — add `IApprovalAuthorityResolver` to the services
  table. Add the new endpoints to the org-units controller table (create
  the table if absent). Extend the `OrgUnit` entity table with `HeadUserId`.
  Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — note the org-chart admin enhancement and the
  approver preview line on the Strategy page. Bump `Last updated:`.

---

## 11. Stop conditions

Stop and ask if:

- The org-chart hierarchy in this database doesn't reach level 3 anywhere
  (the resolver will never find a head — the entire approval workflow is
  blocked).
- The requestor confirms level 1 = leaf (the resolver logic must be
  inverted — walk down, not up — and the meaning of "level 3" changes).
