namespace ActionTracker.Domain.Entities;

/// <summary>
/// Generic document storage entity. Uses polymorphic association
/// (RelatedEntityType + RelatedEntityId) so any entity type
/// (ActionItem, KPI, Project, etc.) can own documents.
/// </summary>
public class Document
{
    public Guid Id { get; set; }

    /// <summary>User-given display name for the document.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Original file name including extension.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME content type (e.g. application/pdf).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes. Max 10 MB.</summary>
    public long FileSize { get; set; }

    /// <summary>Binary file content stored in the database.</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>Type of the owning entity (e.g. "ActionItem", "Kpi", "Project").</summary>
    public string RelatedEntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the owning entity.</summary>
    public Guid RelatedEntityId { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser UploadedBy { get; set; } = null!;
}
