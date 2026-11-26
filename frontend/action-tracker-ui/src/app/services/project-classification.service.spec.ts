import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ProjectClassificationService } from './project-classification.service';
import { environment } from '../../environments/environment';

// WO-2026-11 P-1: routes, and the dropdown cache that every write drops.
describe('ProjectClassificationService (WO-2026-11 P-1)', () => {
  let svc: ProjectClassificationService;
  let http: HttpTestingController;
  const url = `${environment.apiBaseUrl}/workspaces/ws-1/project-classifications`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    svc = TestBed.inject(ProjectClassificationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('calls the workspace routes', () => {
    svc.list('ws-1').subscribe();
    http.expectOne({ method: 'GET', url }).flush({ success: true, data: [] });

    svc.create('ws-1', { name: 'A' }).subscribe();
    const post = http.expectOne({ method: 'POST', url });
    expect(post.request.body).toEqual({ name: 'A' });
    post.flush({ success: true, data: null });

    svc.update('ws-1', 'c1', { name: 'B', isActive: false }).subscribe();
    const put = http.expectOne({ method: 'PUT', url: `${url}/c1` });
    expect(put.request.body).toEqual({ name: 'B', isActive: false });
    put.flush({ success: true, data: null });

    svc.reorder('ws-1', ['c2', 'c1']).subscribe();
    const reorder = http.expectOne({ method: 'PUT', url: `${url}/reorder` });
    expect(reorder.request.body).toEqual({ classificationIds: ['c2', 'c1'] });
    reorder.flush({ success: true, data: [] });

    svc.delete('ws-1', 'c1').subscribe();
    http.expectOne({ method: 'DELETE', url: `${url}/c1` }).flush(null);
  });

  it('caches the options per workspace and drops them on a write', () => {
    let first: unknown[] = [];
    svc.options('ws-1').subscribe(list => first = list);
    http.expectOne(url).flush({ success: true, data: [{ id: 'c1', name: 'A' }] });
    expect(first.length).toBe(1);

    svc.options('ws-1').subscribe();   // from the cache — no request
    http.expectNone(url);

    svc.create('ws-1', { name: 'B' }).subscribe();
    http.expectOne({ method: 'POST', url }).flush({ success: true, data: null });

    svc.options('ws-1').subscribe();
    http.expectOne({ method: 'GET', url }).flush({ success: true, data: [] });
  });

  it('gives an empty list when the options cannot be loaded', () => {
    let result: unknown[] | undefined;
    svc.options('ws-1').subscribe(list => result = list);
    http.expectOne(url).flush({ success: false }, { status: 404, statusText: 'Not Found' });
    expect(result).toEqual([]);

    // Not cached after a failure.
    svc.options('ws-1').subscribe();
    http.expectOne(url).flush({ success: true, data: [] });
  });
});
