using System.Globalization;
using System.Text;
using ActionTracker.Application.Features.ActionItems.DTOs;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace ActionTracker.Application.Helpers;

public class CsvExportHelper
{
    /// <summary>
    /// Serialises <paramref name="items"/> to a UTF-8 CSV byte array (with BOM)
    /// so the file opens correctly in Excel without an import wizard.
    /// </summary>
    public async Task<byte[]> ExportActionItemsToCsvAsync(
        List<ActionItemResponseDto> items, CancellationToken ct)
    {
        var records = items.Select(i => new ActionItemCsvRecord
        {
            Id             = i.Id.ToString(),
            ActionId       = i.ActionId,
            Title          = i.Title,
            Description    = i.Description,
            Workspace      = i.WorkspaceTitle,
            Assignees      = string.Join("; ", i.Assignees.Select(a => a.FullName)),
            Priority       = i.PriorityLabel,
            Status         = i.StatusLabel,
            StartDate      = i.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            DueDate        = i.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Progress       = i.Progress,
            IsEscalated    = i.IsEscalated,
            CreatedAt      = i.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        }).ToList();

        using var memoryStream = new MemoryStream();

        // UTF-8 with BOM (encoderShouldEmitUTF8Identifier: true) for Excel compatibility
        await using var writer = new StreamWriter(
            memoryStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            leaveOpen: true);

        await using var csv = new CsvWriter(
            writer,
            new CsvConfiguration(CultureInfo.InvariantCulture));

        // WriteRecords is synchronous but all data is already in memory
        csv.WriteRecords(records);
        await writer.FlushAsync(ct);

        return memoryStream.ToArray();
    }

    // Private mapping class — controls the CSV column headers via [Name]
    private sealed class ActionItemCsvRecord
    {
        [Name("ID")]
        public string Id { get; set; } = string.Empty;

        [Name("Action ID")]
        public string ActionId { get; set; } = string.Empty;

        [Name("Title")]
        public string Title { get; set; } = string.Empty;

        [Name("Description")]
        public string Description { get; set; } = string.Empty;

        [Name("Workspace")]
        public string Workspace { get; set; } = string.Empty;

        [Name("Assignees")]
        public string Assignees { get; set; } = string.Empty;

        [Name("Priority")]
        public string Priority { get; set; } = string.Empty;

        [Name("Status")]
        public string Status { get; set; } = string.Empty;

        [Name("Start Date")]
        public string StartDate { get; set; } = string.Empty;

        [Name("Due Date")]
        public string DueDate { get; set; } = string.Empty;

        [Name("Progress (%)")]
        public int Progress { get; set; }

        [Name("Escalated")]
        public bool IsEscalated { get; set; }

        [Name("Created At")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}
