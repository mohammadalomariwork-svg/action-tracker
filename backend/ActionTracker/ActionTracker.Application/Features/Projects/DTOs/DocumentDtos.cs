using System;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Projects.DTOs;

/// <summary>
/// Unified read model for a document attached to either a project
/// (<c>ProjectDocument</c>) or an action item (<c>ActionDocument</c>).
/// The caller knows from context which entity the document belongs to.
/// </summary>
public class DocumentDto
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User-supplied display title for the document.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Original file name as uploaded (e.g. "project-charter.pdf").
    /// Shown in the UI when downloading.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type of the uploaded file (e.g. "application/pdf").</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Size of the file in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Display name of the user who uploaded the document.</summary>
    public string UploadedByUserName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the document was uploaded.</summary>
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Payload for uploading a new document.
/// Exactly one of <see cref="ProjectId"/> or <see cref="ActionItemId"/> must be
/// set — this is enforced at the service layer.
/// The actual file bytes are transmitted as a multipart/form-data upload;
/// this DTO carries the metadata fields only.
/// </summary>
public class UploadDocumentDto
{
    /// <summary>
    /// User-supplied display title for the document (required, max 200 chars).
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// FK of the project to attach this document to (nullable).
    /// Set this or <see cref="ActionItemId"/>, not both.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// FK of the action item to attach this document to (nullable).
    /// Set this or <see cref="ProjectId"/>, not both.
    /// </summary>
    public Guid? ActionItemId { get; set; }

    /// <summary>AspNetUsers.Id of the uploader (required).</summary>
    [Required]
    [MaxLength(450)]
    public string UploadedByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the uploader (required).</summary>
    [Required]
    [MaxLength(256)]
    public string UploadedByUserName { get; set; } = string.Empty;
}
