# B-EN-01 — EmailTemplate + EmailLog Entities, EF Configuration, Migration

## Context
We are adding a database-driven email notification system. Email templates are stored in the database (not hard-coded) and can be managed by admins. An email log records every email sent for auditing. This prompt creates entities and migration only.

## Requirements

### 1. Create entity `EmailTemplate` in `ActionTracker.Domain/Entities/`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK, default `Guid.NewGuid()` |
| TemplateKey | string (100) | Unique key, e.g. `ActionItem.Created`, `Project.Completed` |
| Name | string (200) | Human-readable name, e.g. "Action Item Created" |
| Subject | string (500) | Email subject line with placeholders, e.g. `"New Action Item: {{Title}} ({{ActionId}})"` |
| HtmlBody | string (max) | HTML email body with placeholders using `{{PropertyName}}` syntax |
| IsActive | bool | Toggle to disable specific email notifications, default true |
| Description | string? (500) | Admin-facing description of when this template fires |
| CreatedAt | DateTime | UTC |
| UpdatedAt | DateTime | UTC |
| IsDeleted | bool | Soft delete, default false |

### 2. Create entity `EmailLog` in `ActionTracker.Domain/Entities/`

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| TemplateKey | string (100) | Which template was used |
| ToEmail | string (500) | Recipient email address(es), comma-separated if multiple |
| Subject | string (500) | Resolved subject (after placeholder replacement) |
| SentAt | DateTime | UTC timestamp of send |
| Status | string (50) | "Sent", "Failed", "Queued" |
| ErrorMessage | string? (2000) | Error details if failed |
| RelatedEntityType | string? (100) | e.g. "ActionItem", "Project" |
| RelatedEntityId | Guid? | ID of the related entity |
| SentByUserId | string? | User who triggered the action (no FK) |

### 3. Create EF configurations in `ActionTracker.Infrastructure/Data/Configurations/`

**EmailTemplateConfiguration:**
- Table: `EmailTemplates`
- `TemplateKey` has unique index (filtered where `IsDeleted = false`)
- `HtmlBody` mapped to `nvarchar(max)`

**EmailLogConfiguration:**
- Table: `EmailLogs`
- No FKs — all reference fields are plain strings/Guids
- Index on `TemplateKey`
- Index on `SentAt` descending
- Index on `RelatedEntityType` + `RelatedEntityId`

### 4. Register `DbSet<EmailTemplate>` and `DbSet<EmailLog>` in `ApplicationDbContext`

### 5. Create migration
Run: `dotnet ef migrations add AddEmailTemplatesAndLogs --project ActionTracker.Infrastructure --startup-project ActionTracker.Api`

## Rules
- GUID primary keys
- No FK to AspNetUsers
- Soft delete on EmailTemplate; EmailLog is append-only (no soft delete needed)
- UTC for all DateTime fields
- Do NOT create services, DTOs, or controllers in this prompt
