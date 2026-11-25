namespace ActionTracker.Application.Features.Documents.DTOs;

public class DocumentResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string RelatedEntityType { get; set; } = string.Empty;
    public Guid RelatedEntityId { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// True when the caller may delete this file (WO-2026-11 A-4) — the UI shows
    /// the delete button only then. Action items and sub-items: uploader, the
    /// item's creator or assignees, the sub-item's assignees, or an Admin, with
    /// <c>ActionItems.Edit</c>, while the item is not Done / Cancelled. Action
    /// updates: the author of a draft. Other types: as before (anyone who can
    /// see the parent; the KPI org-unit scope still applies on delete).
    /// </summary>
    public bool CanDelete { get; set; }
}

public class DocumentDownloadDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
