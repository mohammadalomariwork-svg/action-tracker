import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { StrategicObjective } from '../models/project.models';

/**
 * Service for managing strategic objectives.
 * Provided at root level — auth headers are added automatically by the HTTP interceptor.
 */
@Injectable({ providedIn: 'root' })
export class StrategicObjectiveService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/strategic-objectives`;

  /**
   * Fetches all strategic objectives.
   * @returns Observable of all strategic objectives.
   */
  getAll(): Observable<StrategicObjective[]> {
    return this.http.get<StrategicObjective[]>(this.apiUrl);
  }

  /**
   * Fetches strategic objectives filtered by organisation unit.
   * @param orgUnit The organisation unit to filter by.
   * @returns Observable of matching strategic objectives.
   */
  getByOrgUnit(orgUnit: string): Observable<StrategicObjective[]> {
    return this.http.get<StrategicObjective[]>(
      `${this.apiUrl}/by-org/${encodeURIComponent(orgUnit)}`
    );
  }

  /**
   * Fetches a single strategic objective by its ID.
   * @param id Primary key of the strategic objective.
   * @returns Observable of the strategic objective.
   */
  getById(id: string): Observable<StrategicObjective> {
    return this.http.get<StrategicObjective>(`${this.apiUrl}/${id}`);
  }

  /**
   * Creates a new strategic objective.
   * @param data Creation payload.
   * @returns Observable of the created strategic objective.
   */
  create(data: Partial<StrategicObjective>): Observable<StrategicObjective> {
    return this.http.post<StrategicObjective>(this.apiUrl, data);
  }

  /**
   * Updates an existing strategic objective.
   * @param id Primary key of the strategic objective to update.
   * @param data Update payload.
   * @returns Observable of the updated strategic objective.
   */
  update(id: string, data: Partial<StrategicObjective>): Observable<StrategicObjective> {
    return this.http.put<StrategicObjective>(`${this.apiUrl}/${id}`, data);
  }

  /**
   * Deletes a strategic objective.
   * @param id Primary key of the strategic objective to delete.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
