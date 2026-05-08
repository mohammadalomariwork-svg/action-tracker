# Prompt: CR-001-C — PMO-Initiated Strategy Setup

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-C
> **Phase:** 1 (Structure)
> **Depends on:** CR-001-A (Strategic Plan entity must exist)
> **Blocks:** CR-001-B (the Strategy page surfaces this flow)

---

## 0. Read first

1. `CLAUDE.md`
2. The output of CR-001-A — `StrategicPlansController`, `IStrategicPlanService`
3. `BACKEND_OVERVIEW.md` — sections on `DefaultRolePermissionsSeeder`,
   `IWorkflowNotificationHelper`, email templates
4. `backend/ActionTracker/ActionTracker.Infrastructure/Notifications/` — the
   existing notification helper (used by project / action-item workflows)
5. `backend/ActionTracker/ActionTracker.Infrastructure/Persistence/Seeders/EmailTemplateSeeder.cs`

---

## 1. Open questions — defaults if unanswered

| # | Default applied |
|---|---|
| Q12 | Notification recipients on plan **creation** (this prompt's scope, not approval): every Strategy Editor whose `OrgUnitId` falls within the plan's org-unit subtree, plus the user who created the plan (cc / record). |

---

## 2. Scope

Tighten the `StrategicPlans.Create` permission so only **PMO Head** and
**Admin** can create a Strategic Plan. Strategy Editors can edit existing
plans within their scope (already granted by CR-001-A) but cannot create
new strategic cycles. On creation, send an in-app + email notification to
the relevant Strategy Editor(s).

---

## 3. Permission tightening

In `DefaultRolePermissionsSeeder`, **revoke** `StrategicPlans.Create` from
any role except `Admin` and `PMO Head`. Concretely:

1. Verify the seeder currently grants `StrategicPlans.Create` to the role
   set you set up in CR-001-A.
2. Remove the row for any role other than Admin / PMO Head.
3. Because the seeder is "insert if missing" by design, add a **one-time
   cleanup** step inside the seeder that, after seeding, deletes rows
   matching `(role IN deprecated set, permission = StrategicPlans.Create)`.
   Mirror the pattern used for the deprecated `Manager` and `User` roles in
   `RoleSeeder` — it deletes idempotently on every startup.

Add a comment explaining why the cleanup exists (so a future maintainer
doesn't remove it).

---

## 4. Notification on plan creation

### 4.1 Notification action type

Add a new `NotificationActionType` enum value: `StrategicPlanCreated`.

If the project has a `NotificationEntityType` enum, add `StrategicPlan` to
it. (See `BACKEND_OVERVIEW.md` for the existing list.)

### 4.2 Email template

Add a new template via `EmailTemplateSeeder`:

- **Key:** `StrategicPlan.Initiated`
- **Subject:** `New strategic plan for {{OrgUnitName}}: {{PlanName}}`
- **Body:** brief HTML explaining a new plan covering `{{StartYear}}–{{EndYear}}`
  has been initiated by `{{CreatedByDisplayName}}`. Include a deep link to
  the plan via `{{PlanUrl}}`.

Match the HTML style of existing templates (`ProjectApproval.Requested`,
`DateChangeRequested`).

### 4.3 Wire-up

In `CreateStrategicPlanCommandHandler` (added by CR-001-A), after the plan
is persisted, call `IWorkflowNotificationHelper` (or the equivalent
notification service used elsewhere) to:

1. Resolve recipients: every active user with `Role = StrategyEditor` whose
   `OrgUnitId` is **inside the plan's org-unit subtree** (use
   `IOrgUnitScopeResolver` to compute the descendant set). De-duplicate.
2. Create an in-app `AppNotification` for each (entity type
   `StrategicPlan`, action type `StrategicPlanCreated`).
3. Send a single SignalR push per recipient.
4. Send the `StrategicPlan.Initiated` email.

If the org unit has zero matching Strategy Editors, log an Information-level
event ("Plan created but no Strategy Editors to notify in subtree of {OrgUnit}")
and continue — do **not** fail the command.

---

## 5. Frontend

### 5.1 Show "Create Plan" button only for PMO Head / Admin

The button to create a new plan must use:

```html
<button *hasPermission="'StrategicPlans.Create'">Create Strategic Plan</button>
```

This is automatic given the directive — the permission was tightened in §3.
Just verify visually after seeding.

### 5.2 Strategy Editor sees "Edit" but no "Create"

When a Strategy Editor lands on the Strategy page (CR-001-B will build that
page), they should see existing plans they can edit, but no "Create" button.
Sanity-check this happens.

### 5.3 Notification rendering

The in-app notification list (already implemented per `status.md` §10) groups
by entity type. Add an icon and colour for `StrategicPlan` matching the
existing convention (e.g. a flag / target icon).

---

## 6. Tests

- `CreateStrategicPlanCommandHandlerTests` — verify notifications fan out to
  Strategy Editors in the subtree, exactly once per user.
- `RolePermissionSeederTests` — assert idempotency and that
  `StrategicPlans.Create` is **only** held by Admin and PMO Head after a
  seeder run.
- Manual: log in as a Strategy Editor and confirm the "Create" button is
  not visible; log in as PMO Head and confirm it is.

---

## 7. Acceptance criteria

1. Only Admin and PMO Head can call `POST /api/strategic-plans` (others get 403).
2. On a successful creation, every Strategy Editor inside the plan's
   org-unit subtree receives one in-app notification and one email.
3. The email template renders with all merge fields populated.
4. Seeder is idempotent — running it twice yields the same final state.
5. No regressions in existing notification tests.

---

## 8. Documentation updates

- `status.md` — under "Strategic Plans" (the section CR-001-A added),
  add: "Plan creation is gated to Admin and PMO Head only. On create, all
  Strategy Editors within the org-unit subtree are notified (in-app + email)."
  Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — add the new email template to the templates list,
  and the new `NotificationActionType` value. Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — no change unless you added an icon mapping.

---

## 9. Stop conditions

Stop and ask if:

- The notification helper does not currently support a "fan-out by org-unit
  subtree" pattern. If so, propose the helper extension before implementing.
- The PMO Head role currently has zero users assigned (the notifications
  will silently no-op — verify this is acceptable in the test environment).
