using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.Models;

/// <summary>
/// Represents a document file attached to a project.
/// Files are stored on disk (or blob storage) under their
/// <see cref="StoredFileName"/>; the <see cref="FileName"/> retains the
/// original name presented to the user.
/// </summary>
public class ProjectDocument
{
    /// <summary>Primary key — auto-incremented integer identity.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key of the project this document is attached to.</summary>
    [Required]
    public int ProjectId { get; set; }

    /// <summary>
    /// User-supplied display title for the document
    /// (e.g. "Kick-off Presentation", "Risk Register v1").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Original file name as uploaded by the user (e.g. "project-charter.pdf").
    /// Shown in the UI when downloading.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// GUID-based name used to store the file on disk / blob storage,
    /// preventing collisions and path-traversal attacks.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the uploaded file (e.g. "application/pdf", "image/png").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Size of the uploaded file in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>AspNetUsers.Id of the user who uploaded the document.</summary>
    [Required]
    [MaxLength(450)]
    public string UploadedByUserId { get; set; } = string.Empty;

    /// <summary>Denormalised display name of the uploader.</summary>
    [Required]
    [MaxLength(256)]
    public string UploadedByUserName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the document was uploaded.</summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Whether this document is active.
    /// <c>false</c> soft-deletes it from queries without removing the stored file.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>The project this document is attached to.</summary>
    public Project Project { get; set; } = null!;
}
