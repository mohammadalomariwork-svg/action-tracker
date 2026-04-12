# F-EN-01 — Admin Email Template Management Page

## Context
The backend exposes `api/email-templates` endpoints (GET list, GET by ID, PUT update, GET logs). This prompt builds the admin page for managing email templates and viewing sent email logs.

## Requirements

### 1. Create models in `src/app/models/email-template.model.ts`

```typescript
export interface EmailTemplate {
  id: string;
  templateKey: string;
  name: string;
  subject: string;
  htmlBody: string;
  isActive: boolean;
  description?: string;
  createdAt: string;
  updatedAt: string;
}

export interface EmailTemplateListItem {
  id: string;
  templateKey: string;
  name: string;
  subject: string;
  isActive: boolean;
  description?: string;
}

export interface UpdateEmailTemplate {
  subject: string;
  htmlBody: string;
  isActive: boolean;
}

export interface EmailLog {
  id: string;
  templateKey: string;
  toEmail: string;
  subject: string;
  sentAt: string;
  status: string;
  errorMessage?: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
}
```

### 2. Create `EmailTemplateService` in `src/app/services/email-template.service.ts`

Methods:
- `getAll(): Observable<ApiResponse<EmailTemplateListItem[]>>`
- `getById(id: string): Observable<ApiResponse<EmailTemplate>>`
- `update(id: string, dto: UpdateEmailTemplate): Observable<ApiResponse<EmailTemplate>>`
- `getLogs(page: number, pageSize: number, templateKey?: string, status?: string): Observable<PagedResult<EmailLog>>`

Base URL: `${environment.apiUrl}/email-templates`

### 3. Create `EmailTemplatesPageComponent` in `src/app/features/admin/email-templates/`

Standalone component. Route: `/admin/email-templates` (add to admin routes, requires AdminOnly guard).

**Layout — Two tabs:**

**Tab 1: Templates**
- Table listing all templates: Name, Template Key, Subject (truncated), Active toggle, Actions
- Active toggle: inline switch that calls `update()` with current subject/body and toggled `isActive`
- Edit button: opens edit modal/offcanvas
- Color code: active templates green, inactive templates gray

**Tab 2: Email Logs**
- Paginated table: Template Key, To Email, Subject, Sent At (formatted with date-fns), Status (badge — Sent=green, Failed=red, Queued=blue), Error (expandable)
- Filters: Template Key dropdown (populated from templates list), Status dropdown
- No create/edit — logs are read-only

### 4. Create `EmailTemplateEditComponent` in `src/app/features/admin/email-template-edit/`

Standalone component. Used as a Bootstrap modal or offcanvas.

**Form fields:**
- Subject (text input, required)
- HTML Body (textarea or simple code editor with monospace font, required, large — at least 15 rows)
- IsActive (checkbox)
- **Preview section:** Render the HTML body in a sandboxed preview area (use `[innerHTML]` with DomSanitizer or an iframe)
- **Available Placeholders:** Show a collapsible reference list of valid `{{Placeholder}}` names for this template (derive from templateKey prefix — e.g., ActionItem templates show ActionItem placeholders)

**Behavior:**
- Load template by ID on open
- On save: call `update()`, show success toast, close modal
- Validate: subject required, htmlBody required

### 5. Add navigation link
Add "Email Templates" to the admin panel navigation/sidebar. This modifies the existing `AdminPanelComponent` or admin routing. Visible only to Admin role.

## Rules
- Standalone components only
- Bootstrap 5 for layout (tabs, tables, badges, modals)
- SCSS, white/light surfaces
- Admin-only access
- Use existing `ApiResponse<T>` and `PagedResult<T>` wrappers
- Strongly typed — no `any`
- ngx-toastr for notifications
