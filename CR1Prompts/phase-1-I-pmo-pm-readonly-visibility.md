# Prompt: CR-001-I — PMO / PM Read-Only Visibility

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-I
> **Phase:** 1 (Structure)
> **Depends on:** nothing (parallel-safe)
> **Blocks:** CR-001-B (Strategy page) needs the read permissions to render
>            the unrestricted org-unit picker for PMO roles

---

## 0. Read first

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — sections on `IStrategicScopeService`,
   `DefaultRolePermissionsSeeder`, `AppRoles`
3. `backend/ActionTracker/ActionTracker.Infrastructure/Services/StrategicScopeService.cs`
4. `backend/ActionTracker/ActionTracker.Infrastructure/Persistence/Seeders/DefaultRolePermissionsSeeder.cs`
5. `status.md` — section 17 (Strategy / KPI Org-Unit Scoping)

---

## 1. Open questions

None blocking. The CR proposal is unambiguous.

---

## 2. Scope

Grant **read-only, cross-org-unit** access to Strategic Plans, Strategic
Objectives, and KPIs to:

- **PMO Head**
- **PMO Analyst**
- **Project Manager** (for picking a strategic objective when creating a
  Strategic Project)

Write paths remain restricted as today — only Strategy Editor (in scope) and
Admin can write.

---

## 3. Permission seeder changes

In `DefaultRolePermissionsSeeder`, ensure the following rows exist (the
seeder is idempotent — only insert missing rows).

### PMO Head

| Area | Action |
|---|---|
| `StrategicPlans` | `View` |
| `StrategicObjectives` | `View` |
| `KPIs` | `View` |

### PMO Analyst

| Area | Action |
|---|---|
| `StrategicPlans` | `View` |
| `StrategicObjectives` | `View` |
| `KPIs` | `View` |

### Project Manager

| Area | Action |
|---|---|
| `StrategicPlans` | `View` |
| `StrategicObjectives` | `View` |

(Project Manager already has `KPIs.View` via existing dashboards — verify
before adding.)

If `StrategicPlans` permissions are not yet present (i.e. CR-001-A hasn't
landed), add them anyway — the area constants are added by CR-001-A and the
seeder runs on every startup.

---

## 4. Scope service changes

In `IStrategicScopeService.GetVisibleOrgUnitIdsAsync(userId)`:

```csharp
// Current behaviour:
//   Admin           → null (unrestricted)
//   StrategyEditor  → user's level-2 ancestor + descendants
//   Other roles     → null (unrestricted)
```

The "Other roles → null" line **already** gives PMO/PM unrestricted *read*
visibility. The change is making this explicit and intentional, not
accidental:

1. Add a constant or comment listing the roles intentionally treated as
   read-unrestricted: `Admin`, `PMO Head`, `PMO Analyst`, `Project Manager`,
   `Workspace Admin`, etc.
2. Add a unit test asserting these roles get `null` (unrestricted) from
   `GetVisibleOrgUnitIdsAsync`.

**Critical:** `EnsureCanWriteAsync` must still **reject writes** from PMO/PM
because they don't hold the `*.Create` / `*.Edit` / `*.Delete` permissions —
the existing permission policy chain handles that. Add a unit test that
proves a PMO Head with `View` only is rejected by an `Edit` policy when
attempting a write through the controller.

---

## 5. Frontend

### 5.1 Permission directives

Wherever a Strategy / KPI list or detail page hides itself behind `Admin` or
`StrategyEditor`-only logic, replace the role check with the permission
directive:

```html
<div *hasPermission="'StrategicObjectives.View'">…</div>
```

Audit:

- `features/kpis/`
- `features/strategic-objectives/` (or wherever they live in the project)
- The header/sidebar nav item that links to the Strategy module

### 5.2 Org-unit picker default

If the user has `null` (unrestricted) scope and the page lands on a
"my org unit" default, fall back to a "select an org unit" empty state
rather than showing nothing or showing all units exploded into one list.
This is consequential for CR-001-B but worth implementing now.

### 5.3 Read-only badges

When the user has `View` but not `Edit`, the existing "Edit" buttons should
be hidden by `*hasPermission` — verify this works for PMO roles by
temporarily logging in as one and checking the strategic objectives page.
No code change expected here, just verification.

---

## 6. Tests

- Backend unit test `StrategicScopeServiceReadVisibilityTests` — explicitly
  asserts each role's read scope.
- Backend integration test: PMO Head `GET /api/strategic-objectives` returns
  rows from multiple org units; same user `POST /api/strategic-objectives`
  returns 403.
- Backend integration test: Project Manager `GET /api/strategic-plans` (once
  CR-001-A endpoints exist) returns 200 across org units.

---

## 7. Acceptance criteria

1. The seeder, run twice, is idempotent — second run inserts zero rows.
2. A PMO Head can list strategic objectives and KPIs from any org unit.
3. The same PMO Head receives 403 on any write to those entities.
4. A Strategy Editor's behaviour is **unchanged** — same scope, same
   permissions, same blocked operations.
5. All existing tests pass.

---

## 8. Documentation updates

- `status.md` — section 17, append a bullet: "PMO Head, PMO Analyst, and
  Project Manager have cross-org-unit **read** access to Strategic Plans,
  Strategic Objectives, and KPIs. Write access remains restricted." Bump
  `Last updated:`.
- `BACKEND_OVERVIEW.md` — under `IStrategicScopeService`, add the explicit
  list of read-unrestricted roles. Update the role-permission summary if it
  shows a matrix. Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — no change required unless you altered components.

---

## 9. Stop conditions

Stop and ask the user if:

- An existing role check in the codebase enforces `Admin`-only on a strategic
  endpoint (rather than a permission policy). The permission system should
  already be authoritative — this would be a latent bug worth flagging
  before broadening access.
