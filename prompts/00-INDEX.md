# Action Tracker — Prompt Package Index

## Three Features
1. **Risk Register** — PMI-compliant project risk management (B-RR-01 to B-RR-03, F-RR-01 to F-RR-03)
2. **Email Notifications** — Database-stored templates + MailKit SMTP delivery (B-EN-01 to B-EN-04, F-EN-01)
3. **In-App Notifications** — Real-time notification center with SignalR (B-IN-01 to B-IN-03, F-IN-01 to F-IN-03)

## Execution Order

Run prompts **strictly in this order**. Each prompt is self-contained. Backend prompts create new files only unless explicitly stated. Frontend prompts are independent of backend file structure.

### Phase 1 — Risk Register
| # | File | Layer | Summary |
|---|------|-------|---------|
| 1 | `B-RR-01.md` | Backend | ProjectRisk entity, enum, EF config, migration |
| 2 | `B-RR-02.md` | Backend | DTOs, validators, service interface + implementation |
| 3 | `B-RR-03.md` | Backend | ProjectRisksController with endpoints |
| 4 | `F-RR-01.md` | Frontend | Models, service, routing |
| 5 | `F-RR-02.md` | Frontend | Risk list component inside project detail |
| 6 | `F-RR-03.md` | Frontend | Risk form (create/edit) + risk detail modal |

### Phase 2 — Email Notifications
| # | File | Layer | Summary |
|---|------|-------|---------|
| 7 | `B-EN-01.md` | Backend | EmailTemplate + EmailLog entities, EF config, migration |
| 8 | `B-EN-02.md` | Backend | IEmailTemplateService + IEmailSender (MailKit), config |
| 9 | `B-EN-03.md` | Backend | EmailTemplateSeeder — seed all default templates |
| 10 | `B-EN-04.md` | Backend | Wire email sending into existing services (ActionItem, Project, Workspace, etc.) |
| 11 | `F-EN-01.md` | Frontend | Admin email template management page |

### Phase 3 — In-App Notifications
| # | File | Layer | Summary |
|---|------|-------|---------|
| 12 | `B-IN-01.md` | Backend | AppNotification entity, EF config, migration |
| 13 | `B-IN-02.md` | Backend | INotificationService + SignalR NotificationHub |
| 14 | `B-IN-03.md` | Backend | Wire notification creation into existing services |
| 15 | `F-IN-01.md` | Frontend | Notification model + service + SignalR connection |
| 16 | `F-IN-02.md` | Frontend | Notification bell + dropdown in header |
| 17 | `F-IN-03.md` | Frontend | Notifications page (full list, mark-read, filters) |

### Post-Implementation
| # | File | Layer | Summary |
|---|------|-------|---------|
| 18 | `B-PERM-01.md` | Backend | Add Notifications + EmailTemplates permission areas to catalog seeder |
