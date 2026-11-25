import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ActionUpdateService } from './action-update.service';
import { environment } from '../../environments/environment';

// WO-2026-11 A-3: the same calls at action-item and sub-item level.
describe('ActionUpdateService (WO-2026-11 A-3)', () => {
  let service: ActionUpdateService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/action-items/A`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ActionUpdateService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses /updates for the action item and /sub-items/{id}/updates for a sub-item', () => {
    service.list('A', null).subscribe();
    http.expectOne({ method: 'GET', url: `${base}/updates` }).flush({ data: null });

    service.list('A', 'S').subscribe();
    http.expectOne({ method: 'GET', url: `${base}/sub-items/S/updates` }).flush({ data: null });

    service.counts('A').subscribe();
    http.expectOne({ method: 'GET', url: `${base}/updates/counts` }).flush({ data: null });
  });

  it('creates, edits, submits and deletes', () => {
    service.create('A', 'S', { content: 'x', isHighImportance: true, submit: false }).subscribe();
    const create = http.expectOne({ method: 'POST', url: `${base}/sub-items/S/updates` });
    expect(create.request.body).toEqual({ content: 'x', isHighImportance: true, submit: false });
    create.flush({ data: null });

    service.update('A', null, 'U', { content: 'y', isHighImportance: false }).subscribe();
    http.expectOne({ method: 'PUT', url: `${base}/updates/U` }).flush({ data: null });

    service.submit('A', null, 'U').subscribe();
    http.expectOne({ method: 'POST', url: `${base}/updates/U/submit` }).flush({ data: null });

    service.delete('A', 'S', 'U').subscribe();
    http.expectOne({ method: 'DELETE', url: `${base}/sub-items/S/updates/U` }).flush(null);
  });
});
