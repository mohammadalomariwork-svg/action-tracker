# B-PW-07 — Permission Catalog Seed: Projects.Approve

## Context

The permission catalog already has 15 areas and 7 actions. The `Approve` action exists in the catalog. However, we need to verify that the `Projects` area has an `AreaPermissionMapping` entry for the `Approve` action. If it doesn't exist, we need to add it. Additionally, default role permissions should grant `Projects.Approve` to `Admin` and `Manager` roles.

## What to do

### 1. Check and add `Projects.Approve` mapping

In the existing `PermissionCatalogSeeder` (or equivalent startup seeder):
- Look for the area-action mapping where `AreaName = "Projects"` and `ActionName = "Approve"`
- If it does not exist, add a new `AreaPermissionMapping` record linking the `Projects` area to the `Approve` action
- Use a hardcoded GUID for idempotent seeding (matching the convention of other mappings)

### 2. Add default role permissions

In the existing `DefaultRolePermissionsSeeder` (or equivalent):
- Add `Projects.Approve` permission for the `Admin` role (if not already present)
- Add `Projects.Approve` permission for the `Manager` role (if not already present)
- Use hardcoded GUIDs for the `RolePermission` records

### 3. Add authorization policy constant

In the existing `PermissionPolicies` constants class (or wherever policy names are defined):
- Ensure `"Projects.Approve"` is included in the list of auto-generated policy names
- This should already work if policies are dynamically generated from the catalog, but verify

### 4. Update `PendingApproval` handling in `ProjectStatus` for UI display

In the frontend models (next prompt handles this), the `PendingApproval` status needs to be recognized. On the backend, verify that any place that converts `ProjectStatus` to a display string handles the new `PendingApproval` value correctly (e.g., status badge mappings, Swagger enum documentation).

## Files to modify
- Permission catalog seeder file — add `Projects` × `Approve` mapping if missing
- Default role permissions seeder file — add `Projects.Approve` for Admin and Manager roles
- Verify `PermissionPolicies` constants include `Projects.Approve`

## Files to create
- None

## Do NOT
- Do not delete or modify existing permission mappings
- Do not modify the permission entities
- Do not change the authorization handler logic (it already works dynamically from the catalog)
