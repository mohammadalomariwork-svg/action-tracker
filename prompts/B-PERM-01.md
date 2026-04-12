# B-PERM-01 — Add Notifications + EmailTemplates Permission Areas to Catalog Seeder

## Context
We have added three new features: Risk Register, Email Templates, and In-App Notifications. The permission catalog needs new areas so that admins can control access to these features via the existing permission system.

## Requirements

### 1. Add new permission areas to `PermissionCatalogSeeder`

This modifies the existing `PermissionCatalogSeeder` file.

**New areas to add (with stable hardcoded GUIDs):**

| Area Name | Description | Valid Actions |
|-----------|-------------|--------------|
| `Risks` | Project risk register management | View, Create, Edit, Delete, Export |
| `EmailTemplates` | Email template administration | View, Edit |
| `Notifications` | In-app notifications | View, Delete |

### 2. Add area-action mappings

In the same seeder, add `AreaPermissionMapping` entries linking each new area to its valid actions. Use stable hardcoded GUIDs for each mapping.

### 3. Add default role permissions in `DefaultRolePermissionsSeeder`

This modifies the existing `DefaultRolePermissionsSeeder` file.

| Role | Risks | EmailTemplates | Notifications |
|------|-------|----------------|---------------|
| Admin | All (View, Create, Edit, Delete, Export) | View, Edit | View, Delete |
| Manager | View, Create, Edit, Export | — | View, Delete |
| User | View | — | View, Delete |
| Viewer | View | — | View |

### 4. Add authorization policies

In the existing authorization policy configuration (where dynamic policies are generated from `PermissionPolicies` constants):

Add the new area constants:
- `Risks.View`, `Risks.Create`, `Risks.Edit`, `Risks.Delete`, `Risks.Export`
- `EmailTemplates.View`, `EmailTemplates.Edit`
- `Notifications.View`, `Notifications.Delete`

This may require adding entries to a `PermissionPolicies` constants class or wherever the existing 12 areas are defined.

### 5. Update existing controllers to use new permissions

**Modify `ProjectRisksController` (created in B-RR-03):**
- Change from `Projects.View` / `Projects.Edit` / `Projects.Delete` to `Risks.View` / `Risks.Create` / `Risks.Edit` / `Risks.Delete`

**Modify `EmailTemplatesController` (created in B-EN-02):**
- Change from `AdminOnly` to `EmailTemplates.View` for GET endpoints and `EmailTemplates.Edit` for PUT endpoint

**Modify `NotificationsController` (created in B-IN-02):**
- Keep as `Authenticated` for GET/PATCH endpoints (all users can see their own notifications)
- Use `Notifications.Delete` for DELETE endpoints

### 6. Update frontend permission areas

This modifies the frontend `PermissionArea` enum/constant in `src/app/models/` to include the three new areas: `Risks`, `EmailTemplates`, `Notifications`.

Update any permission guards or `hasPermission()` checks in the newly created frontend components to use the correct area names.

## Rules
- Use stable hardcoded GUIDs (generate new ones, do not reuse existing)
- Upsert logic in seeders — do not overwrite existing data
- Follow the exact same seeder pattern as the existing 12 areas
- Update the total from "12 permission areas" to "15 permission areas" in any relevant constants or comments
- This prompt modifies existing seeder files, policy config, and controllers
