using System.Collections.Generic;
using System.Threading.Tasks;
using ActionTracker.Application.Features.Projects.DTOs;
using Microsoft.AspNetCore.Http;

namespace ActionTracker.Application.Features.Projects.Interfaces;

/// <summary>
/// Defines the application-level operations for uploading, retrieving,
/// downloading, and deleting documents attached to projects and action items.
/// File bytes are persisted to disk or blob storage; this service manages
/// both the physical file and its metadata record.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Uploads a document and associates it with a project.
    /// The physical file is stored under a GUID-based name to prevent
    /// collisions and path-traversal attacks.
    /// </summary>
    /// <param name="dto">Metadata for the document (title, uploader, project ID).</param>
    /// <param name="file">The file stream received from the HTTP multipart request.</param>
    /// <returns>The created document metadata record.</returns>
    Task<DocumentDto> UploadProjectDocumentAsync(UploadDocumentDto dto, IFormFile file);

    /// <summary>
    /// Uploads a document and associates it with an action item.
    /// The physical file is stored under a GUID-based name to prevent
    /// collisions and path-traversal attacks.
    /// </summary>
    /// <param name="dto">Metadata for the document (title, uploader, action-item ID).</param>
    /// <param name="file">The file stream received from the HTTP multipart request.</param>
    /// <returns>The created document metadata record.</returns>
    Task<DocumentDto> UploadActionDocumentAsync(UploadDocumentDto dto, IFormFile file);

    /// <summary>
    /// Returns all active documents attached to the specified project.
    /// </summary>
    /// <param name="projectId">Primary key of the project.</param>
    Task<IEnumerable<DocumentDto>> GetByProjectAsync(int projectId);

    /// <summary>
    /// Returns all active documents attached to the specified action item.
    /// </summary>
    /// <param name="actionItemId">Primary key of the action item.</param>
    Task<IEnumerable<DocumentDto>> GetByActionItemAsync(int actionItemId);

    /// <summary>
    /// Reads a document from storage and returns its raw bytes together with
    /// the content type and original file name required for an HTTP file-
    /// download response.
    /// </summary>
    /// <param name="documentId">Primary key of the document record.</param>
    /// <param name="isProjectDocument">
    /// <c>true</c> to look up a <c>ProjectDocument</c>;
    /// <c>false</c> to look up an <c>ActionDocument</c>.
    /// </param>
    /// <returns>
    /// A tuple of (<c>bytes</c>, <c>contentType</c>, <c>fileName</c>).
    /// </returns>
    Task<(byte[] bytes, string contentType, string fileName)> DownloadAsync(
        int documentId,
        bool isProjectDocument);

    /// <summary>
    /// Soft-deletes a project document.  Only the uploader or a workspace
    /// admin may perform this operation.
    /// </summary>
    /// <param name="documentId">Primary key of the project document to delete.</param>
    /// <param name="requestingUserId">
    /// AspNetUsers.Id of the user making the request.
    /// </param>
    /// <returns>
    /// <c>true</c> if the document was found and deleted; <c>false</c> if not
    /// found or not authorised.
    /// </returns>
    Task<bool> DeleteProjectDocumentAsync(int documentId, string requestingUserId);

    /// <summary>
    /// Soft-deletes an action-item document.  Only the uploader or a workspace
    /// admin may perform this operation.
    /// </summary>
    /// <param name="documentId">Primary key of the action document to delete.</param>
    /// <param name="requestingUserId">
    /// AspNetUsers.Id of the user making the request.
    /// </param>
    /// <returns>
    /// <c>true</c> if the document was found and deleted; <c>false</c> if not
    /// found or not authorised.
    /// </returns>
    Task<bool> DeleteActionDocumentAsync(int documentId, string requestingUserId);
}
