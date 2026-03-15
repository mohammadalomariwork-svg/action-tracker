import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  AppPermissionAreaDto,
  AppPermissionActionDto,
  AreaActionMappingDto,
  CreateAreaDto,
  CreateActionDto,
} from '../models/permission-catalog.model';

@Injectable({ providedIn: 'root' })
export class PermissionCatalogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl + '/permission-catalog';

  getAreas(): Observable<AppPermissionAreaDto[]> {
    return this.http.get<AppPermissionAreaDto[]>(`${this.baseUrl}/areas`);
  }

  getActions(): Observable<AppPermissionActionDto[]> {
    return this.http.get<AppPermissionActionDto[]>(`${this.baseUrl}/actions`);
  }

  getMappings(): Observable<AreaActionMappingDto[]> {
    return this.http.get<AreaActionMappingDto[]>(`${this.baseUrl}/mappings`);
  }

  getMappingsByArea(areaId: string): Observable<AreaActionMappingDto[]> {
    return this.http.get<AreaActionMappingDto[]>(`${this.baseUrl}/mappings/by-area/${areaId}`);
  }

  createArea(dto: CreateAreaDto): Observable<AppPermissionAreaDto> {
    return this.http.post<AppPermissionAreaDto>(`${this.baseUrl}/areas`, dto);
  }

  updateArea(id: string, dto: CreateAreaDto): Observable<AppPermissionAreaDto> {
    return this.http.put<AppPermissionAreaDto>(`${this.baseUrl}/areas/${id}`, dto);
  }

  deleteArea(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/areas/${id}`);
  }

  createAction(dto: CreateActionDto): Observable<AppPermissionActionDto> {
    return this.http.post<AppPermissionActionDto>(`${this.baseUrl}/actions`, dto);
  }

  updateAction(id: string, dto: CreateActionDto): Observable<AppPermissionActionDto> {
    return this.http.put<AppPermissionActionDto>(`${this.baseUrl}/actions/${id}`, dto);
  }

  deleteAction(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/actions/${id}`);
  }

  createMapping(dto: { areaId: string; actionId: string }): Observable<AreaActionMappingDto> {
    return this.http.post<AreaActionMappingDto>(`${this.baseUrl}/mappings`, dto);
  }

  deleteMapping(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/mappings/${id}`);
  }
}
