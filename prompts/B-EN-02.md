# B-EN-02 — Email Template Service + Email Sender (MailKit)

## Context
B-EN-01 created EmailTemplate and EmailLog entities. This prompt builds the service layer: template CRUD, placeholder resolution, and SMTP sending via MailKit.

## Requirements

### 1. Install NuGet package
Add `MailKit` (latest stable compatible with .NET 9) to `ActionTracker.Infrastructure` project.

### 2. Add SMTP configuration to `appsettings.json`
```json
"Smtp": {
  "Host": "smtp.office365.com",
  "Port": 587,
  "UseSsl": true,
  "FromEmail": "noreply@ku.ac.ae",
  "FromName": "KU Action Tracker",
  "Username": "",
  "Password": ""
}
```

### 3. Create `SmtpSettings` class in `ActionTracker.Application/Common/`
Strongly typed options class matching the JSON above. Register with `services.Configure<SmtpSettings>(configuration.GetSection("Smtp"))`.

### 4. Create DTOs in `ActionTracker.Application/Features/EmailTemplates/DTOs/`

**EmailTemplateDto** (response):
- Id, TemplateKey, Name, Subject, HtmlBody, IsActive, Description, CreatedAt, UpdatedAt

**UpdateEmailTemplateDto** (request — admins can edit subject, body, isActive):
- Subject (required), HtmlBody (required), IsActive (bool)

**EmailTemplateListDto** (lightweight for list):
- Id, TemplateKey, Name, Subject (truncated to 100 chars), IsActive, Description

**EmailLogDto** (response):
- Id, TemplateKey, ToEmail, Subject, SentAt, Status, ErrorMessage, RelatedEntityType, RelatedEntityId

### 5. Create `IEmailTemplateService` in `ActionTracker.Application/Features/EmailTemplates/`

```
Task<List<EmailTemplateListDto>> GetAllAsync();
Task<EmailTemplateDto?> GetByIdAsync(Guid id);
Task<EmailTemplateDto?> GetByKeyAsync(string templateKey);
Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateDto dto);
Task<PagedResult<EmailLogDto>> GetLogsAsync(int page, int pageSize, string? templateKey, string? status);
```

Note: Templates are seeded, not created by users. Admins can only edit subject, body, and active status.

### 6. Create `IEmailSender` in `ActionTracker.Application/Common/`

```
Task SendEmailAsync(string templateKey, Dictionary<string, string> placeholders, List<string> recipientEmails, string? relatedEntityType = null, Guid? relatedEntityId = null, string? triggeredByUserId = null);
```

### 7. Create `EmailTemplateService` in `ActionTracker.Infrastructure/Services/`

Implements `IEmailTemplateService`:
- Standard CRUD for templates (query from DB)
- Logs query with pagination and filtering

### 8. Create `EmailSender` in `ActionTracker.Infrastructure/Services/`

Implements `IEmailSender`:

**SendEmailAsync logic:**
1. Load template by `templateKey` from DB. If not found or `IsActive = false`, log warning and return silently (do not throw).
2. Resolve placeholders: replace all `{{Key}}` tokens in Subject and HtmlBody with values from the dictionary. Unreplaced placeholders should be replaced with empty string.
3. Wrap the resolved HTML body in a base email layout template (stored as a constant or embedded resource):
   - KU branding header with logo placeholder
   - Content area
   - Footer with "This is an automated message from KU Action Tracker. Please do not reply."
4. Send via MailKit SMTP:
   - Create `MimeMessage` with From, To (support multiple recipients), Subject, HTML body
   - Connect to SMTP using configured host/port/SSL
   - Authenticate with username/password
   - Send
5. Log result to `EmailLog` table:
   - Status = "Sent" on success
   - Status = "Failed" + ErrorMessage on exception
6. **Do NOT throw exceptions on email failure** — log the error and continue. Email failures must never break the main business operation.

### 9. Register services in DI
- `IEmailTemplateService` → `EmailTemplateService`
- `IEmailSender` → `EmailSender`
- `SmtpSettings` options binding

### 10. Create `EmailTemplatesController` in `ActionTracker.Api/Controllers/`

Route: `api/email-templates`

| Method | Route | Permission | Description |
|--------|-------|-----------|-------------|
| GET | `/` | AdminOnly | List all templates |
| GET | `/{id}` | AdminOnly | Get template by ID |
| PUT | `/{id}` | AdminOnly | Update template (subject, body, isActive) |
| GET | `/logs` | AdminOnly | Paginated email logs. Query params: page, pageSize, templateKey, status |

## Rules
- All async
- MailKit is the SMTP library (not System.Net.Mail)
- Email failures are caught, logged, and swallowed — never thrown
- Templates use `{{Placeholder}}` syntax
- No FK to AspNetUsers in EmailLog
- SmtpSettings loaded from appsettings.json via IOptions<SmtpSettings>
