# B-WF-07 — Permission Catalog Update for Workflow

## Context

The permission system uses `AppPermissionArea`, `AppPermissionAction`, and `AreaPermissionMapping` catalog tables, seeded by `PermissionCatalogSeeder` on startup. The `ActionItems` area already has mappings for `View`, `Create`, `Edit`, `Delete`, `Export`, `Assign` actions.

The workflow controller uses `ActionItems.Edit` for creating requests and `ActionItems.Approve` for reviewing them. We need to ensure the `Approve` action is mapped to the `ActionItems` area.

## Pre-requisite

- B-WF-06 completed

## Instructions

### 1. Check if `ActionItems.Approve` mapping exists

Look at the `PermissionCatalogSeeder` (or `DefaultRolePermissionsSeeder`). The 7 actions are: `View, Create, Edit, Delete, Approve, Export, Assign`. The `Approve` action likely exists in the `AppPermissionAction` seed, but check if there is an `AreaPermissionMapping` row for `ActionItems` + `Approve`.

### 2. If missing, add the mapping

In the permission catalog seeder, add an `AreaPermissionMapping` entry connecting the `ActionItems` area to the `Approve` action. Use a hardcoded stable GUID like the existing mappings do.

### 3. Assign default permissions

In the `DefaultRolePermissionsSeeder`, add `ActionItems.Approve` to these roles:
- **Admin** — can approve anything
- **Manager** — can approve as a manager

Do NOT give `Approve` to the `User` or `Viewer` roles by default. Users who are action item creators or direct managers will be able to approve based on the workflow service logic, but the endpoint still requires the `ActionItems.Approve` permission at the policy level.

**Important consideration:** Since any user who is a creator or manager should be able to approve, but the policy requires `ActionItems.Approve`, you have two options:

**Option A (Recommended):** Change the controller's review endpoint to use `ActionItems.Edit` instead of `ActionItems.Approve`, since the service layer already validates whether the specific user can review a specific request. This way, any user with `Edit` permission can attempt to review, and the service returns 403 if they aren't authorized for that specific request.

**Option B:** Use `ActionItems.Approve` and give it to the `User` role by default, relying on the service-layer check for the specific authorization.

Choose Option A unless there's a clear reason to use a separate `Approve` permission. If choosing Option A, update the controller endpoint from B-WF-06 to use `ActionItems.Edit` for the review endpoint.

### 4. Add `PermissionPolicies` constant

Check the `PermissionPolicies` constants file (likely in `ActionTracker.Domain/Constants/` or `ActionTracker.Application/Constants/`). If there are explicit policy name constants, ensure `ActionItemsApprove` exists (or verify it's auto-generated from the catalog). This step may not be needed if policies are dynamically generated from the permission catalog.

## Validation

- Application starts and seeds without errors
- The permission matrix in the admin UI shows the correct mappings
- The `ActionItems.Approve` (or `ActionItems.Edit`) policy works on the workflow endpoints
