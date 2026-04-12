# F-TH-15 — User Management: UserList, RegisterAdUser, RegisterExternalUser

## Requirements

### 1. `UserListComponent` SCSS
- DataTable theme (automatic): columns — Name, Email, Role badge, Org Unit, Login Provider badge (AD=accent, Local=gray), Active status (green/red dot), Actions
- Search input: themed from F-TH-06
- Pagination: themed from F-TH-08
- Action buttons: ghost icon buttons
- Mobile: card view at <600px

### 2. `RegisterAdUserComponent` SCSS
- Card wrapper, max-width 600px centered
- Employee search input: themed input with autocomplete dropdown (`var(--el)` bg, `var(--bd)` border, `var(--r-md)` radius)
- Search results list: `var(--sf)` bg rows, hover `var(--sfh)`
- Selected employee card: `var(--el)` bg with avatar + details
- Submit button: neon primary

### 3. `RegisterExternalUserComponent` SCSS
- Card wrapper, max-width 600px centered
- Form: themed inputs (email, password, first name, last name, department)
- Two-column on desktop, single column mobile
- Submit button: neon primary

## Rules
- SCSS only per component
- No changes to user registration logic, employee search API calls, or form validation
