import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiBaseUrl}/reports`;

  exportCsv(filter: any): Observable<Blob> {
    let params = new HttpParams();
    for (const key of Object.keys(filter)) {
      if (filter[key] != null && filter[key] !== '') {
        params = params.set(key, filter[key]);
      }
    }
    return this.http.get(`${this.apiUrl}/export/csv`, { params, responseType: 'blob' });
  }
}
