using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Features.Projects.Interfaces;
using ActionTracker.Application.Features.Projects.Models;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Application.Features.Projects.Services;

/// <summary>
/// Application service for uploading, retrieving, downloading, and deleting
/// documents attached to projects and action items.
/// Physical files are stored on the local file system under the path configured
/// in <c>appsettings.json</c> at <c>"FileStorage:Path"</c>.
/// </summary>
public class DocumentService : IDocumentService
{
    /// <summary>Maximum permitted file upload size (20 MB).</summary>
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>
    /// File extensions permitted for upload.
    /// Content-type is validated in addition to the extension.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".xlsx", ".pptx", ".jpg", ".jpeg", ".png", ".txt"
        };

    /// <summary>
    /// MIME types that correspond to the allowed extensions.
    /// Both extension and content-type must be in the permitted sets.
    /// </summary>
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "image/jpeg",
            "image/png",
            "text/plain"
        };

    private readonly IAppDbContext _db;
    private readonly ILogger<DocumentService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly string _storageBasePath;

    /// <summary>
    /// Initialises the service.  The storage root is read from
    /// <c>configuration["FileStorage:Path"]</c>; falls back to
    /// <c>wwwroot/uploads/documents</c> relative to the current directory
    /// when the key is absent.
    /// </summary>
    public DocumentService(
        IAppDbContext db,
        ILogger<DocumentService> logger,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _db          = db;
        _logger      = logger;
        _userManager = userManager;
        _storageBasePath = configuration["FileStorage:Path"]
                        ?? Path.Combine(Directory.GetCurrentDirectory(),
                                        "wwwroot", "uploads", "documents");
    }

    // ── Uploads ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// File is saved under <c>{storageBase}/projects/{projectId}/</c>.
    /// A <c>ProjectDocument</c> metadata record is persisted to the database.
    /// </remarks>
    public async Task<DocumentDto> UploadProjectDocumentAsync(UploadDocumentDto dto, IFormFile file)
    {
        ValidateFile(file);

        int projectId = dto.ProjectId
            ?? throw new ArgumentException("ProjectId is required for a project document upload.",
                                           nameof(dto.ProjectId));

        try
        {
            string extension     = Path.GetExtension(file.FileName).ToLowerInvariant();
            string storedName    = $"{Guid.NewGuid()}{extension}";
            string folder        = Path.Combine(_storageBasePath, "projects", projectId.ToString());

            Directory.CreateDirectory(folder);

            string fullPath = Path.Combine(folder, storedName);
            await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                await file.CopyToAsync(stream);

            var doc = new ProjectDocument
            {
                ProjectId           = projectId,
                Title               = dto.Title,
                FileName            = file.FileName,
                StoredFileName      = storedName,
                ContentType         = file.ContentType,
                FileSizeBytes       = file.Length,
                UploadedByUserId    = dto.UploadedByUserId,
                UploadedByUserName  = dto.UploadedByUserName,
                UploadedAt          = DateTime.UtcNow,
                IsActive            = true
            };

            _db.ProjectDocuments.Add(doc);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Uploaded project document {Id} '{FileName}' to project {ProjectId}.",
                doc.Id, file.FileName, projectId);

            return MapToDto(doc);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex,
                "Error uploading project document '{FileName}' to project {ProjectId}.",
                file.FileName, projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// File is saved under <c>{storageBase}/actions/{actionItemId}/</c>.
    /// An <c>ActionDocument</c> metadata record is persisted to the database.
    /// </remarks>
    public async Task<DocumentDto> UploadActionDocumentAsync(UploadDocumentDto dto, IFormFile file)
    {
        ValidateFile(file);

        int actionItemId = dto.ActionItemId
            ?? throw new ArgumentException("ActionItemId is required for an action document upload.",
                                           nameof(dto.ActionItemId));

        try
        {
            string extension  = Path.GetExtension(file.FileName).ToLowerInvariant();
            string storedName = $"{Guid.NewGuid()}{extension}";
            string folder     = Path.Combine(_storageBasePath, "actions", actionItemId.ToString());

            Directory.CreateDirectory(folder);

            string fullPath = Path.Combine(folder, storedName);
            await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                await file.CopyToAsync(stream);

            var doc = new ActionDocument
            {
                ActionItemId        = actionItemId,
                Title               = dto.Title,
                FileName            = file.FileName,
                StoredFileName      = storedName,
                ContentType         = file.ContentType,
                FileSizeBytes       = file.Length,
                UploadedByUserId    = dto.UploadedByUserId,
                UploadedByUserName  = dto.UploadedByUserName,
                UploadedAt          = DateTime.UtcNow,
                IsActive            = true
            };

            _db.ActionDocuments.Add(doc);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Uploaded action document {Id} '{FileName}' to action item {ActionItemId}.",
                doc.Id, file.FileName, actionItemId);

            return MapToDto(doc);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex,
                "Error uploading action document '{FileName}' to action item {ActionItemId}.",
                file.FileName, actionItemId);
            throw;
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentDto>> GetByProjectAsync(int projectId)
    {
        try
        {
            var docs = await _db.ProjectDocuments
                .Where(d => d.ProjectId == projectId && d.IsActive)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return docs.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving documents for project {ProjectId}.", projectId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentDto>> GetByActionItemAsync(int actionItemId)
    {
        try
        {
            var docs = await _db.ActionDocuments
                .Where(d => d.ActionItemId == actionItemId && d.IsActive)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return docs.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving documents for action item {ActionItemId}.", actionItemId);
            throw;
        }
    }

    // ── Download ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the database record exists but the physical file cannot be
    /// found on disk.
    /// </exception>
    public async Task<(byte[] bytes, string contentType, string fileName)> DownloadAsync(
        int documentId, bool isProjectDocument)
    {
        try
        {
            string storedName, contentType, fileName, subfolder;

            if (isProjectDocument)
            {
                var doc = await _db.ProjectDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive)
                    ?? throw new FileNotFoundException(
                           $"Project document {documentId} not found.");

                storedName  = doc.StoredFileName;
                contentType = doc.ContentType;
                fileName    = doc.FileName;
                subfolder   = Path.Combine("projects", doc.ProjectId.ToString());
            }
            else
            {
                var doc = await _db.ActionDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive)
                    ?? throw new FileNotFoundException(
                           $"Action document {documentId} not found.");

                storedName  = doc.StoredFileName;
                contentType = doc.ContentType;
                fileName    = doc.FileName;
                subfolder   = Path.Combine("actions", doc.ActionItemId.ToString());
            }

            string fullPath = Path.Combine(_storageBasePath, subfolder, storedName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"Physical file for document {documentId} not found on disk.", fullPath);

            byte[] bytes = await File.ReadAllBytesAsync(fullPath);

            _logger.LogInformation("Downloaded document {DocumentId} ({IsProject}).",
                documentId, isProjectDocument ? "project" : "action");

            return (bytes, contentType, fileName);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}.", documentId);
            throw;
        }
    }

    // ── Deletes ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Soft-deletes the record (<c>IsActive = false</c>). The physical file is
    /// also removed from disk when the deletion succeeds.
    /// Only the uploader or a user in the <c>Admin</c> role may delete.
    /// </remarks>
    public async Task<bool> DeleteProjectDocumentAsync(int documentId, string requestingUserId)
    {
        try
        {
            var doc = await _db.ProjectDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive);

            if (doc is null) return false;

            if (!await IsAuthorOrAdmin(doc.UploadedByUserId, requestingUserId))
            {
                _logger.LogWarning(
                    "User {UserId} attempted to delete project document {DocId} without permission.",
                    requestingUserId, documentId);
                return false;
            }

            string fullPath = Path.Combine(
                _storageBasePath, "projects", doc.ProjectId.ToString(), doc.StoredFileName);

            doc.IsActive = false;
            await _db.SaveChangesAsync();

            TryDeletePhysicalFile(fullPath);

            _logger.LogInformation("User {UserId} deleted project document {DocId}.",
                requestingUserId, documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project document {DocId}.", documentId);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Soft-deletes the record (<c>IsActive = false</c>). The physical file is
    /// also removed from disk when the deletion succeeds.
    /// Only the uploader or a user in the <c>Admin</c> role may delete.
    /// </remarks>
    public async Task<bool> DeleteActionDocumentAsync(int documentId, string requestingUserId)
    {
        try
        {
            var doc = await _db.ActionDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive);

            if (doc is null) return false;

            if (!await IsAuthorOrAdmin(doc.UploadedByUserId, requestingUserId))
            {
                _logger.LogWarning(
                    "User {UserId} attempted to delete action document {DocId} without permission.",
                    requestingUserId, documentId);
                return false;
            }

            string fullPath = Path.Combine(
                _storageBasePath, "actions", doc.ActionItemId.ToString(), doc.StoredFileName);

            doc.IsActive = false;
            await _db.SaveChangesAsync();

            TryDeletePhysicalFile(fullPath);

            _logger.LogInformation("User {UserId} deleted action document {DocId}.",
                requestingUserId, documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting action document {DocId}.", documentId);
            throw;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates <paramref name="file"/> size and content type against
    /// permitted values.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the file is empty, exceeds <see cref="MaxFileSizeBytes"/>, or
    /// has a disallowed extension / content-type.
    /// </exception>
    private static void ValidateFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("No file provided or file is empty.", nameof(file));

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException(
                $"File size {file.Length:N0} bytes exceeds the 20 MB limit.", nameof(file));

        string extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException(
                $"File extension '{extension}' is not permitted. Allowed: {string.Join(", ", AllowedExtensions)}.",
                nameof(file));

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException(
                $"Content type '{file.ContentType}' is not permitted.",
                nameof(file));
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="requestingUserId"/> matches the
    /// resource owner or the user is in the <c>Admin</c> role.
    /// </summary>
    private async Task<bool> IsAuthorOrAdmin(string ownerUserId, string requestingUserId)
    {
        if (ownerUserId == requestingUserId) return true;

        var user = await _userManager.FindByIdAsync(requestingUserId);
        if (user is null) return false;

        return await _userManager.IsInRoleAsync(user, "Admin");
    }

    /// <summary>
    /// Attempts to delete a physical file from disk, logging a warning on
    /// failure without re-throwing so the DB soft-delete is not rolled back.
    /// </summary>
    private void TryDeletePhysicalFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not delete physical file '{Path}'. Manual cleanup may be required.", fullPath);
        }
    }

    // ── Private mapping ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="ProjectDocument"/> to a <see cref="DocumentDto"/>.</summary>
    private static DocumentDto MapToDto(ProjectDocument d) => new()
    {
        Id                 = d.Id,
        Title              = d.Title,
        FileName           = d.FileName,
        ContentType        = d.ContentType,
        FileSizeBytes      = d.FileSizeBytes,
        UploadedByUserName = d.UploadedByUserName,
        UploadedAt         = d.UploadedAt
    };

    /// <summary>Maps an <see cref="ActionDocument"/> to a <see cref="DocumentDto"/>.</summary>
    private static DocumentDto MapToDto(ActionDocument d) => new()
    {
        Id                 = d.Id,
        Title              = d.Title,
        FileName           = d.FileName,
        ContentType        = d.ContentType,
        FileSizeBytes      = d.FileSizeBytes,
        UploadedByUserName = d.UploadedByUserName,
        UploadedAt         = d.UploadedAt
    };
}
