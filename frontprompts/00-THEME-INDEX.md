# Actions Center — Dark Neon Theme Prompts Index

## Reference
- `DARK-NEON-SPEC.md` — Complete design specification. **Claude Code must read this before executing any prompt.**

## Critical Rule
**Every prompt modifies ONLY SCSS and minimal HTML (adding CSS classes or wrapper divs). No TypeScript, no data bindings, no routing, no guards, no services, no content changes.**

## Execution Order

Run strictly in this order. After each prompt, verify the app compiles and renders.

```
git checkout -b feature/dark-neon-theme
```

### Phase 1 — Theme Foundation
| # | File | Summary |
|---|------|---------|
| 1 | `F-TH-01.md` | SCSS variables, dark/light CSS custom properties, global resets, font, scrollbar, selection |
| 2 | `F-TH-02.md` | Animated background (particles, waves, orbs, diagonals), border rails, corner brackets |
| 3 | `F-TH-03.md` | Theme toggle service + localStorage persistence |

### Phase 2 — Layout Shell
| # | File | Summary |
|---|------|---------|
| 4 | `F-TH-04.md` | Header — full-width, logo swap, nav links, hamburger menu, mobile off-canvas drawer |
| 5 | `F-TH-05.md` | Footer — full-width, glass treatment. LayoutComponent — wrap with side margins and borders |

### Phase 3 — Shared Components
| # | File | Summary |
|---|------|---------|
| 6 | `F-TH-06.md` | Cards, buttons (neon glow), inputs, selects, textareas — global form overrides |
| 7 | `F-TH-07.md` | StatusBadge, PriorityBadge, ProgressBar, KpiCard, Breadcrumb, PageHeader |
| 8 | `F-TH-08.md` | DataTable, pagination, ConfirmDialog, LoadingBar, CommentsSectionComponent, DocumentsSectionComponent |

### Phase 4 — Feature Pages
| # | File | Summary |
|---|------|---------|
| 9 | `F-TH-09.md` | TeamDashboard, ManagementDashboard — stat cards grid, charts, lists |
| 10 | `F-TH-10.md` | ActionList, ActionForm, ActionDetail — table, mobile cards, filters, form |
| 11 | `F-TH-11.md` | MyProjects, ProjectForm, ProjectDetail, MilestoneSection, MilestoneDetail, Gantt chart |
| 12 | `F-TH-12.md` | WorkspaceList, WorkspaceForm (offcanvas), WorkspaceDetail — cards grid, tabs |
| 13 | `F-TH-13.md` | AdminPanel, OrgChartList, OrgUnitForm, ObjectivesList, ObjectiveForm, KpiList, KpiForm, KpiTargets |
| 14 | `F-TH-14.md` | RolePermissionsPage, RolePermissionMatrix, UserOverridesPage, RolesListPage, RoleUsersPage |
| 15 | `F-TH-15.md` | UserList, RegisterAdUser, RegisterExternalUser |
| 16 | `F-TH-16.md` | LoginComponent, UnauthorizedComponent, AccessDeniedComponent |
| 17 | `F-TH-17.md` | NotificationBell, NotificationsPage (if created from notification prompts) |

### Phase 5 — Polish
| # | File | Summary |
|---|------|---------|
| 18 | `F-TH-18.md` | Toasts, modals, offcanvas, dropdowns, tooltips, print styles, reduced-motion, focus-visible |
