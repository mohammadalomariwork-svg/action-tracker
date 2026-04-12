# F-TH-14 — Permissions & Roles Pages

## Requirements

### 1. `RolePermissionsPageComponent` + `RolePermissionMatrixComponent` + `RolePermissionRowComponent` SCSS
- Card wrapper for the entire matrix
- Role selector: pill tabs at top (`var(--sf)` bg, active `rgba(var(--ar), 0.08)`)
- Matrix table: DataTable theme (headers, rows, borders)
- Checkboxes: custom styled — `var(--accent)` fill when checked, `var(--bd)` border unchecked
- Row headers (area names): `font-weight: 600; color: var(--t1)` with icon
- Column headers (action names): `font-size: 9px; text-transform: uppercase; color: var(--t3)`
- Save button: neon primary, sticky at bottom of card

### 2. `UserOverridesPageComponent` + `UserOverrideFormComponent` + `UserOverridesListComponent` SCSS
- User search: themed input with dropdown suggestion list (`var(--el)` bg, `var(--bd)` border)
- Overrides table: DataTable theme
- Grant badge: green pill. Deny badge: red pill.
- Add override modal: card-elevated treatment
- Form inputs: themed from F-TH-06

### 3. `UserEffectivePermissionsSummaryComponent` SCSS
- Card wrapper, themed info-rows listing effective permissions
- Granted: green text/icon. Denied: red text/icon.

### 4. `RolesListPageComponent` SCSS
- Role cards: grid layout (3 cols desktop, 2 tablet, 1 mobile)
- Each card: `var(--sf)` bg, role name, user count, "Manage" button
- `.card-glow:hover`

### 5. `RoleUsersPageComponent` SCSS
- Split layout: two cards side by side (assigned vs available users)
- User rows: avatar + name + email
- Transfer buttons between lists: neon primary (add) / ghost (remove)
- Mobile: stacked vertically

## Rules
- SCSS only per component
- Permission matrix checkbox logic, role assignment logic all untouched
- No TypeScript changes
