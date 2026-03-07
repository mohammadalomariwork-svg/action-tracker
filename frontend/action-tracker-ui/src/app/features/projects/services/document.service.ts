import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { DocumentInfo } from '../models/project.models';

/**
 * Service for managing project and action-item document uploads and downloads.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/documents`;

  /**
   * Fetches all documents for a given project.
   * @param projectId The project to fetch documents for.
   * @returns Observable of document metadata.
   */
  getByProject(projectId: number): Observable<DocumentInfo[]> {
    return this.http.get<DocumentInfo[]>(`${this.apiUrl}/project/${projectId}`);
  }

  /**
   * Fetches all documents for a given action item.
   * @param actionItemId The action item to fetch documents for.
   * @returns Observable of document metadata.
   */
  getByActionItem(actionItemId: number): Observable<DocumentInfo[]> {
    return this.http.get<DocumentInfo[]>(`${this.apiUrl}/action-item/${actionItemId}`);
  }

  /**
   * Uploads a document to a project.
   * @param projectId The project to attach the document to.
   * @param title Display title of the document.
   * @param file The file to upload.
   * @returns Observable of the created document metadata.
   */
  uploadProjectDocument(projectId: number, title: string, file: File): Observable<DocumentInfo> {
    const formData = new FormData();
    formData.append('title', title);
    formData.append('projectId', projectId.toString());
    formData.append('file', file);
    return this.http.post<DocumentInfo>(`${this.apiUrl}/project`, formData);
  }

  /**
   * Uploads a document to an action item.
   * @param actionItemId The action item to attach the document to.
   * @param title Display title of the document.
   * @param file The file to upload.
   * @returns Observable of the created document metadata.
   */
  uploadActionDocument(actionItemId: number, title: string, file: File): Observable<DocumentInfo> {
    const formData = new FormData();
    formData.append('title', title);
    formData.append('actionItemId', actionItemId.toString());
    formData.append('file', file);
    return this.http.post<DocumentInfo>(`${this.apiUrl}/action-item`, formData);
  }

  /**
   * Downloads a document as a binary blob.
   * @param id Primary key of the document.
   * @param type Whether the document belongs to a project or an action item.
   * @returns Observable of the file blob.
   */
  downloadDocument(id: number, type: 'project' | 'action'): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download`, {
      params: { type },
      responseType: 'blob',
    });
  }

  /**
   * Deletes a project document.
   * @param id Primary key of the document to delete.
   */
  deleteProjectDocument(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/project/${id}`);
  }

  /**
   * Deletes an action-item document.
   * @param id Primary key of the document to delete.
   */
  deleteActionDocument(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/action-item/${id}`);
  }
}
