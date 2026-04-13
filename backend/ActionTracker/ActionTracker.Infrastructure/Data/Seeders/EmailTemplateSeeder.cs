using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data.Seeders;

public static class EmailTemplateSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var existingKeys = await db.EmailTemplates
            .IgnoreQueryFilters()
            .Select(t => t.TemplateKey)
            .ToListAsync();

        var templates = BuildTemplates();
        var toInsert = templates.Where(t => !existingKeys.Contains(t.TemplateKey)).ToList();

        if (toInsert.Count == 0)
        {
            logger.LogInformation("EmailTemplateSeeder: all {Count} templates already exist — skipping.", templates.Count);
            return;
        }

        db.EmailTemplates.AddRange(toInsert);

        // Update existing templates whose body has changed (e.g. escalation template)
        var templateLookup = templates.ToDictionary(t => t.TemplateKey);
        var existingTemplates = await db.EmailTemplates
            .IgnoreQueryFilters()
            .Where(t => existingKeys.Contains(t.TemplateKey))
            .ToListAsync();

        var updatedCount = 0;
        foreach (var existing in existingTemplates)
        {
            if (templateLookup.TryGetValue(existing.TemplateKey, out var seed)
                && existing.HtmlBody != seed.HtmlBody)
            {
                existing.HtmlBody = seed.HtmlBody;
                existing.Subject = seed.Subject;
                updatedCount++;
            }
        }

        await db.SaveChangesAsync();

        if (toInsert.Count > 0)
            logger.LogInformation("EmailTemplateSeeder: inserted {Count} new email templates.", toInsert.Count);
        if (updatedCount > 0)
            logger.LogInformation("EmailTemplateSeeder: updated {Count} existing email templates.", updatedCount);
    }

    private static List<EmailTemplate> BuildTemplates()
    {
        var now = DateTime.UtcNow;

        return
        [
            // ── Action Item templates ────────────────────────────────────────
            new()
            {
                Id          = new Guid("a1b2c3d4-0001-4000-8000-000000000001"),
                TemplateKey = "ActionItem.Created",
                Name        = "Action Item Created",
                Subject     = "New Action Item: {{Title}} ({{ActionId}})",
                Description = "Sent when a new action item is created.",
                HtmlBody    = BuildActionItemBody(
                    "New Action Item Created",
                    "A new action item has been created and requires your attention."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a1b2c3d4-0002-4000-8000-000000000002"),
                TemplateKey = "ActionItem.Assigned",
                Name        = "Action Item Assigned",
                Subject     = "You've been assigned: {{Title}} ({{ActionId}})",
                Description = "Sent when a user is assigned to an action item.",
                HtmlBody    = BuildActionItemBody(
                    "You've Been Assigned an Action Item",
                    "You have been assigned to the following action item."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a1b2c3d4-0003-4000-8000-000000000003"),
                TemplateKey = "ActionItem.StatusChanged",
                Name        = "Action Item Status Changed",
                Subject     = "Action Item {{ActionId}} status changed to {{Status}}",
                Description = "Sent when an action item status changes.",
                HtmlBody    = BuildActionItemBody(
                    "Action Item Status Updated",
                    "The status of the following action item has been updated."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a1b2c3d4-0004-4000-8000-000000000004"),
                TemplateKey = "ActionItem.Completed",
                Name        = "Action Item Completed",
                Subject     = "Action Item Completed: {{Title}} ({{ActionId}})",
                Description = "Sent when an action item status changes to Done.",
                HtmlBody    = BuildActionItemBody(
                    "Action Item Completed",
                    "The following action item has been marked as completed."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a1b2c3d4-0005-4000-8000-000000000005"),
                TemplateKey = "ActionItem.Overdue",
                Name        = "Action Item Overdue",
                Subject     = "Action Item Overdue: {{Title}} ({{ActionId}})",
                Description = "Sent when an action item is marked overdue.",
                HtmlBody    = BuildActionItemBody(
                    "Action Item Overdue",
                    "The following action item is now past its due date."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a1b2c3d4-0006-4000-8000-000000000006"),
                TemplateKey = "ActionItem.Escalated",
                Name        = "Action Item Escalated",
                Subject     = "Action Item Escalated: {{Title}} ({{ActionId}})",
                Description = "Sent when an action item is escalated.",
                HtmlBody    = BuildEscalationBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("d4e5f6a7-b8c9-4d01-a234-567890abcde1"),
                TemplateKey = "ActionItem.DateChangeRequested",
                Name        = "Date Change Requested",
                Subject     = "Date Change Requested — {{ActionId}}: {{Title}}",
                Description = "Sent when a date change is requested for an action item.",
                HtmlBody    = BuildDateChangeRequestedBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("d4e5f6a7-b8c9-4d01-a234-567890abcde2"),
                TemplateKey = "ActionItem.DateChangeReviewed",
                Name        = "Date Change Reviewed",
                Subject     = "Date Change {{Outcome}} — {{ActionId}}: {{Title}}",
                Description = "Sent when a date change request is approved or rejected.",
                HtmlBody    = BuildDateChangeReviewedBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("d4e5f6a7-b8c9-4d01-a234-567890abcde3"),
                TemplateKey = "ActionItem.StatusChangeRequested",
                Name        = "Status Change Requested",
                Subject     = "Status Change Requested — {{ActionId}}: {{Title}}",
                Description = "Sent when a status change is requested for an action item.",
                HtmlBody    = BuildStatusChangeRequestedBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("d4e5f6a7-b8c9-4d01-a234-567890abcde4"),
                TemplateKey = "ActionItem.StatusChangeReviewed",
                Name        = "Status Change Reviewed",
                Subject     = "Status Change {{Outcome}} — {{ActionId}}: {{Title}}",
                Description = "Sent when a status change request is approved or rejected.",
                HtmlBody    = BuildStatusChangeReviewedBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("d4e5f6a7-b8c9-4d01-a234-567890abcde5"),
                TemplateKey = "ActionItem.DirectionGiven",
                Name        = "Direction Given",
                Subject     = "Direction Given — {{ActionId}}: {{Title}}",
                Description = "Sent when a direction is given on an action item.",
                HtmlBody    = BuildDirectionGivenBody(),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Project templates ───────────────────────────────���────────────
            new()
            {
                Id          = new Guid("b2c3d4e5-0001-4000-8000-000000000001"),
                TemplateKey = "Project.Created",
                Name        = "Project Created",
                Subject     = "New Project: {{ProjectName}} ({{ProjectCode}})",
                Description = "Sent when a project is created.",
                HtmlBody    = BuildProjectBody(
                    "New Project Created",
                    "A new project has been created."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("b2c3d4e5-0002-4000-8000-000000000002"),
                TemplateKey = "Project.StatusChanged",
                Name        = "Project Status Changed",
                Subject     = "Project {{ProjectCode}} status changed to {{Status}}",
                Description = "Sent when a project status changes.",
                HtmlBody    = BuildProjectBody(
                    "Project Status Updated",
                    "The status of the following project has been updated."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("b2c3d4e5-0003-4000-8000-000000000003"),
                TemplateKey = "Project.Completed",
                Name        = "Project Completed",
                Subject     = "Project Completed: {{ProjectName}} ({{ProjectCode}})",
                Description = "Sent when a project status changes to Completed.",
                HtmlBody    = BuildProjectBody(
                    "Project Completed",
                    "The following project has been completed."),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Milestone templates ──────────────────────────────────────────
            new()
            {
                Id          = new Guid("c3d4e5f6-0001-4000-8000-000000000001"),
                TemplateKey = "Milestone.Created",
                Name        = "Milestone Created",
                Subject     = "New Milestone: {{MilestoneName}} ({{MilestoneCode}})",
                Description = "Sent when a milestone is created.",
                HtmlBody    = BuildMilestoneBody(
                    "New Milestone Created",
                    "A new milestone has been created."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("c3d4e5f6-0002-4000-8000-000000000002"),
                TemplateKey = "Milestone.Completed",
                Name        = "Milestone Completed",
                Subject     = "Milestone Completed: {{MilestoneName}} ({{MilestoneCode}})",
                Description = "Sent when a milestone is completed.",
                HtmlBody    = BuildMilestoneBody(
                    "Milestone Completed",
                    "The following milestone has been completed."),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Project Approval Workflow templates ──────────────────────────
            new()
            {
                Id          = new Guid("b2c3d4e5-0004-4000-8000-000000000004"),
                TemplateKey = "ProjectApproval.Requested",
                Name        = "Project Approval Requested",
                Subject     = "Project Approval Requested: {{ProjectCode}} — {{ProjectName}}",
                Description = "Sent to sponsors and direct line manager when a project is submitted for start-approval.",
                HtmlBody    = BuildProjectApprovalRequestedBody(),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("b2c3d4e5-0005-4000-8000-000000000005"),
                TemplateKey = "ProjectApproval.Reviewed",
                Name        = "Project Approval Reviewed",
                Subject     = "Project {{Decision}}: {{ProjectCode}} — {{ProjectName}}",
                Description = "Sent to the project manager after a sponsor or manager approves or rejects the request.",
                HtmlBody    = BuildProjectApprovalReviewedBody(),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Workspace template ───────────────────────────────────────────
            new()
            {
                Id          = new Guid("d4e5f6a7-0001-4000-8000-000000000001"),
                TemplateKey = "Workspace.Created",
                Name        = "Workspace Created",
                Subject     = "New Workspace: {{WorkspaceName}}",
                Description = "Sent when a workspace is created.",
                HtmlBody    = BuildWorkspaceBody(),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Strategic Objective template ─────────────────────────────────
            new()
            {
                Id          = new Guid("e5f6a7b8-0001-4000-8000-000000000001"),
                TemplateKey = "StrategicObjective.Created",
                Name        = "Strategic Objective Created",
                Subject     = "New Strategic Objective: {{ObjectiveCode}} — {{Statement}}",
                Description = "Sent when a strategic objective is created.",
                HtmlBody    = BuildStrategicObjectiveBody(),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── KPI template ───────────────────────��─────────────────────────
            new()
            {
                Id          = new Guid("f6a7b8c9-0001-4000-8000-000000000001"),
                TemplateKey = "Kpi.Created",
                Name        = "KPI Created",
                Subject     = "New KPI: {{KpiName}} for {{ObjectiveName}}",
                Description = "Sent when a KPI is created.",
                HtmlBody    = BuildKpiBody(),
                IsActive  = true,
                CreatedAt = now,
            },

            // ── Risk templates ───────────────────────────────────────────────
            new()
            {
                Id          = new Guid("a7b8c9d0-0001-4000-8000-000000000001"),
                TemplateKey = "Risk.Created",
                Name        = "Risk Created",
                Subject     = "New Risk: {{RiskCode}} — {{Title}} ({{ProjectName}})",
                Description = "Sent when a risk is identified.",
                HtmlBody    = BuildRiskBody(
                    "New Risk Identified",
                    "A new risk has been identified for the following project."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a7b8c9d0-0002-4000-8000-000000000002"),
                TemplateKey = "Risk.StatusChanged",
                Name        = "Risk Status Changed",
                Subject     = "Risk {{RiskCode}} status changed to {{Status}}",
                Description = "Sent when a risk status changes.",
                HtmlBody    = BuildRiskBody(
                    "Risk Status Updated",
                    "The status of the following risk has been updated."),
                IsActive  = true,
                CreatedAt = now,
            },
            new()
            {
                Id          = new Guid("a7b8c9d0-0003-4000-8000-000000000003"),
                TemplateKey = "Risk.Critical",
                Name        = "Critical Risk Alert",
                Subject     = "Critical Risk Alert: {{RiskCode}} — {{Title}}",
                Description = "Sent when a risk is rated Critical.",
                HtmlBody    = BuildRiskBody(
                    "Critical Risk Alert",
                    "The following risk has been rated as <strong>Critical</strong> and requires immediate attention."),
                IsActive  = true,
                CreatedAt = now,
            },
        ];
    }

    // ── HTML body builders ───────────────────────────────────────────────────

    private static string BuildActionItemBody(string heading, string intro) =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        $"<h2 style=\"color: #0F52BA;\">{heading}</h2>" +
        $"<p>{intro}</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 140px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Description:</td><td style=\"padding: 8px;\">{{Description}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Status:</td><td style=\"padding: 8px;\">{{Status}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Priority:</td><td style=\"padding: 8px;\">{{Priority}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Due Date:</td><td style=\"padding: 8px;\">{{DueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Progress:</td><td style=\"padding: 8px;\">{{Progress}}%</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Assigned To:</td><td style=\"padding: 8px;\">{{AssignedTo}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Created By:</td><td style=\"padding: 8px;\">{{CreatedBy}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Workspace:</td><td style=\"padding: 8px;\">{{WorkspaceName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project:</td><td style=\"padding: 8px;\">{{ProjectName}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ItemUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Details</a>" +
        "</div>";

    private static string BuildEscalationBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #dc3545;\">Action Item Escalated</h2>" +
        "<p>The following action item has been escalated and requires immediate attention.</p>" +
        "<div style=\"background-color: #fff3cd; border: 1px solid #ffc107; border-radius: 6px; padding: 12px 16px; margin: 16px 0;\">" +
        "<p style=\"margin: 0 0 4px 0; font-weight: 600;\">Escalated By:</p>" +
        "<p style=\"margin: 0 0 12px 0;\">{{EscalatedBy}}</p>" +
        "<p style=\"margin: 0 0 4px 0; font-weight: 600;\">Escalation Reason:</p>" +
        "<p style=\"margin: 0;\">{{Reason}}</p>" +
        "</div>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 140px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Description:</td><td style=\"padding: 8px;\">{{Description}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Status:</td><td style=\"padding: 8px;\">{{Status}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Priority:</td><td style=\"padding: 8px;\">{{Priority}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Due Date:</td><td style=\"padding: 8px;\">{{DueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Progress:</td><td style=\"padding: 8px;\">{{Progress}}%</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Assigned To:</td><td style=\"padding: 8px;\">{{AssignedTo}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Created By:</td><td style=\"padding: 8px;\">{{CreatedBy}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Workspace:</td><td style=\"padding: 8px;\">{{WorkspaceName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project:</td><td style=\"padding: 8px;\">{{ProjectName}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ItemUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #dc3545; color: #fff; text-decoration: none; border-radius: 4px;\">View Details</a>" +
        "</div>";

    private static string BuildProjectBody(string heading, string intro) =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        $"<h2 style=\"color: #0F52BA;\">{heading}</h2>" +
        $"<p>{intro}</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 140px;\">Project Code:</td><td style=\"padding: 8px;\">{{ProjectCode}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project Name:</td><td style=\"padding: 8px;\">{{ProjectName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Description:</td><td style=\"padding: 8px;\">{{Description}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Status:</td><td style=\"padding: 8px;\">{{Status}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Priority:</td><td style=\"padding: 8px;\">{{Priority}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project Manager:</td><td style=\"padding: 8px;\">{{ProjectManager}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Workspace:</td><td style=\"padding: 8px;\">{{WorkspaceName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Planned Start:</td><td style=\"padding: 8px;\">{{PlannedStartDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Planned End:</td><td style=\"padding: 8px;\">{{PlannedEndDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Budget:</td><td style=\"padding: 8px;\">{{Budget}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ItemUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Details</a>" +
        "</div>";

    private static string BuildMilestoneBody(string heading, string intro) =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        $"<h2 style=\"color: #0F52BA;\">{heading}</h2>" +
        $"<p>{intro}</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 140px;\">Milestone Code:</td><td style=\"padding: 8px;\">{{MilestoneCode}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Milestone Name:</td><td style=\"padding: 8px;\">{{MilestoneName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project:</td><td style=\"padding: 8px;\">{{ProjectName}} ({{ProjectCode}})</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Status:</td><td style=\"padding: 8px;\">{{Status}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Planned Due Date:</td><td style=\"padding: 8px;\">{{PlannedDueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Completion:</td><td style=\"padding: 8px;\">{{CompletionPercentage}}%</td></tr>" +
        "</table>" +
        "<a href=\"{{ItemUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Details</a>" +
        "</div>";

    private static string BuildWorkspaceBody() => """
        <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;">
          <h2 style="color: #0F52BA;">New Workspace Created</h2>
          <p>A new workspace has been created.</p>
          <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
            <tr><td style="padding: 8px; font-weight: 600; width: 140px;">Workspace:</td><td style="padding: 8px;">{{WorkspaceName}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Organization Unit:</td><td style="padding: 8px;">{{OrgUnit}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Created By:</td><td style="padding: 8px;">{{CreatedBy}}</td></tr>
          </table>
          <a href="{{ItemUrl}}" style="display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;">View Details</a>
        </div>
        """;

    private static string BuildStrategicObjectiveBody() => """
        <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;">
          <h2 style="color: #0F52BA;">New Strategic Objective Created</h2>
          <p>A new strategic objective has been created.</p>
          <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
            <tr><td style="padding: 8px; font-weight: 600; width: 140px;">Code:</td><td style="padding: 8px;">{{ObjectiveCode}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Statement:</td><td style="padding: 8px;">{{Statement}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Organization Unit:</td><td style="padding: 8px;">{{OrgUnit}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Created By:</td><td style="padding: 8px;">{{CreatedBy}}</td></tr>
          </table>
          <a href="{{ItemUrl}}" style="display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;">View Details</a>
        </div>
        """;

    private static string BuildKpiBody() => """
        <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;">
          <h2 style="color: #0F52BA;">New KPI Created</h2>
          <p>A new KPI has been created.</p>
          <table style="width: 100%; border-collapse: collapse; margin: 16px 0;">
            <tr><td style="padding: 8px; font-weight: 600; width: 140px;">KPI Name:</td><td style="padding: 8px;">{{KpiName}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Objective:</td><td style="padding: 8px;">{{ObjectiveName}} ({{ObjectiveCode}})</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Calculation Method:</td><td style="padding: 8px;">{{CalculationMethod}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Period:</td><td style="padding: 8px;">{{Period}}</td></tr>
            <tr><td style="padding: 8px; font-weight: 600;">Created By:</td><td style="padding: 8px;">{{CreatedBy}}</td></tr>
          </table>
          <a href="{{ItemUrl}}" style="display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;">View Details</a>
        </div>
        """;

    private static string BuildRiskBody(string heading, string intro) =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        $"<h2 style=\"color: #0F52BA;\">{heading}</h2>" +
        $"<p>{intro}</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 140px;\">Risk Code:</td><td style=\"padding: 8px;\">{{RiskCode}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Description:</td><td style=\"padding: 8px;\">{{Description}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Category:</td><td style=\"padding: 8px;\">{{Category}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Risk Score:</td><td style=\"padding: 8px;\">{{RiskScore}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Risk Rating:</td><td style=\"padding: 8px;\">{{RiskRating}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Status:</td><td style=\"padding: 8px;\">{{Status}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project:</td><td style=\"padding: 8px;\">{{ProjectName}} ({{ProjectCode}})</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Risk Owner:</td><td style=\"padding: 8px;\">{{RiskOwner}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Due Date:</td><td style=\"padding: 8px;\">{{DueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Mitigation Plan:</td><td style=\"padding: 8px;\">{{MitigationPlan}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ItemUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Details</a>" +
        "</div>";

    // ── Change-request & direction body builders ────────────────────────────

    private static string BuildDateChangeRequestedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Date Change Requested</h2>" +
        "<p>Hi {{RecipientName}},</p>" +
        "<p><strong>{{RequesterName}}</strong> has requested a date change for the following action item.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Start Date:</td><td style=\"padding: 8px;\">{{CurrentStartDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Proposed Start Date:</td><td style=\"padding: 8px;\">{{NewStartDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Due Date:</td><td style=\"padding: 8px;\">{{CurrentDueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Proposed Due Date:</td><td style=\"padding: 8px;\">{{NewDueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Reason:</td><td style=\"padding: 8px;\">{{Reason}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ApprovalsUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">Review Request</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildDateChangeReviewedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Date Change {{Outcome}}</h2>" +
        "<p>Hi {{RecipientName}},</p>" +
        "<p>Your date change request has been <strong>{{Outcome}}</strong> by <strong>{{ReviewerName}}</strong>.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Start Date:</td><td style=\"padding: 8px;\">{{CurrentStartDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Requested Start Date:</td><td style=\"padding: 8px;\">{{NewStartDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Due Date:</td><td style=\"padding: 8px;\">{{CurrentDueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Requested Due Date:</td><td style=\"padding: 8px;\">{{NewDueDate}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Outcome:</td><td style=\"padding: 8px;\">{{Outcome}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Reviewer Comment:</td><td style=\"padding: 8px;\">{{ReviewComment}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ActionUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Action Item</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildStatusChangeRequestedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Status Change Requested</h2>" +
        "<p>Hi {{RecipientName}},</p>" +
        "<p><strong>{{RequesterName}}</strong> has requested a status change for the following action item.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Status:</td><td style=\"padding: 8px;\">{{CurrentStatus}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Proposed Status:</td><td style=\"padding: 8px;\">{{NewStatus}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Reason:</td><td style=\"padding: 8px;\">{{Reason}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ApprovalsUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">Review Request</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildStatusChangeReviewedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Status Change {{Outcome}}</h2>" +
        "<p>Hi {{RecipientName}},</p>" +
        "<p>Your status change request has been <strong>{{Outcome}}</strong> by <strong>{{ReviewerName}}</strong>.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Current Status:</td><td style=\"padding: 8px;\">{{CurrentStatus}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Requested Status:</td><td style=\"padding: 8px;\">{{NewStatus}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Outcome:</td><td style=\"padding: 8px;\">{{Outcome}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Reviewer Comment:</td><td style=\"padding: 8px;\">{{ReviewComment}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ActionUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Action Item</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildProjectApprovalRequestedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Project Approval Requested</h2>" +
        "<p>Hi,</p>" +
        "<p><strong>{{RequestedByName}}</strong> has submitted a project for your approval.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Project Code:</td><td style=\"padding: 8px;\">{{ProjectCode}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project Name:</td><td style=\"padding: 8px;\">{{ProjectName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Reason:</td><td style=\"padding: 8px;\">{{Reason}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ProjectUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Project</a>" +
        "&nbsp;&nbsp;" +
        "<a href=\"{{ApprovalUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #28a745; color: #fff; text-decoration: none; border-radius: 4px;\">Review Request</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildProjectApprovalReviewedBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Project {{Decision}}</h2>" +
        "<p>Hi,</p>" +
        "<p>Your project approval request has been <strong>{{Decision}}</strong> by <strong>{{ReviewedByName}}</strong>.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Project Code:</td><td style=\"padding: 8px;\">{{ProjectCode}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Project Name:</td><td style=\"padding: 8px;\">{{ProjectName}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Decision:</td><td style=\"padding: 8px;\">{{Decision}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Comment:</td><td style=\"padding: 8px;\">{{ReviewComment}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ProjectUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Project</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";

    private static string BuildDirectionGivenBody() =>
        "<div style=\"font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto;\">" +
        "<h2 style=\"color: #0F52BA;\">Direction Given</h2>" +
        "<p>Hi {{RecipientName}},</p>" +
        "<p><strong>{{DirectorName}}</strong> has provided direction on the following action item.</p>" +
        "<table style=\"width: 100%; border-collapse: collapse; margin: 16px 0;\">" +
        "<tr><td style=\"padding: 8px; font-weight: 600; width: 160px;\">Action ID:</td><td style=\"padding: 8px;\">{{ActionId}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Title:</td><td style=\"padding: 8px;\">{{Title}}</td></tr>" +
        "<tr><td style=\"padding: 8px; font-weight: 600;\">Direction:</td><td style=\"padding: 8px;\">{{Reason}}</td></tr>" +
        "</table>" +
        "<a href=\"{{ActionUrl}}\" style=\"display: inline-block; padding: 10px 24px; background-color: #0F52BA; color: #fff; text-decoration: none; border-radius: 4px;\">View Action Item</a>" +
        "<p style=\"margin-top: 24px; font-size: 12px; color: #666;\">This is an automated message from KU Action Tracker.</p>" +
        "</div>";
}
