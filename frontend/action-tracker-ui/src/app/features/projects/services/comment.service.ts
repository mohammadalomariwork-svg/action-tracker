import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Comment, CreateComment } from '../models/project.models';

/**
 * Service for managing comments on projects, milestones, and action items.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class CommentService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/comments`;

  /**
   * Fetches all comments for a given action item.
   * @param id Primary key of the action item.
   * @returns Observable of comments.
   */
  getByActionItem(id: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.apiUrl}/action-item/${id}`);
  }

  /**
   * Fetches all comments for a given milestone.
   * @param id Primary key of the milestone.
   * @returns Observable of comments.
   */
  getByMilestone(id: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.apiUrl}/milestone/${id}`);
  }

  /**
   * Fetches all comments for a given project.
   * @param id Primary key of the project.
   * @returns Observable of comments.
   */
  getByProject(id: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.apiUrl}/project/${id}`);
  }

  /**
   * Creates a new comment.
   * @param data Creation payload.
   * @returns Observable of the created comment.
   */
  create(data: CreateComment): Observable<Comment> {
    return this.http.post<Comment>(this.apiUrl, data);
  }

  /**
   * Updates an existing comment's content.
   * @param id Primary key of the comment to update.
   * @param content The new comment content.
   * @returns Observable of the updated comment.
   */
  update(id: string, content: string): Observable<Comment> {
    return this.http.put<Comment>(`${this.apiUrl}/${id}`, { content });
  }

  /**
   * Deletes a comment.
   * @param id Primary key of the comment to delete.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
