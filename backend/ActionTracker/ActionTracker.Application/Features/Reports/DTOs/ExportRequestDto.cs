using ActionTracker.Domain.Enums;

namespace ActionTracker.Application.Features.Reports.DTOs;

public class ExportRequestDto
{
    public ActionStatus?   Status     { get; set; }
    public ActionPriority? Priority   { get; set; }
    public string?         AssigneeId { get; set; }

    /// <summary>Filter ActionItems whose DueDate is on or after this date.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Filter ActionItems whose DueDate is on or before this date.</summary>
    public DateTime? DateTo { get; set; }
}
