import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { WorkflowService } from './workflow.service';
import { environment } from '../../environments/environment';

// GET /action-items/workflow/can-review/{id} answers ApiResponse<bool>: `data` is the
// boolean itself. The action page's Review buttons depend on reading it as such.
describe('WorkflowService.canReview', () => {
  let service: WorkflowService;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/action-items/workflow/can-review/A1`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(WorkflowService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function answer(body: object): boolean | undefined {
    let result: boolean | undefined;
    service.canReview('A1').subscribe(v => (result = v));
    http.expectOne({ method: 'GET', url }).flush(body);
    return result;
  }

  it('is true when the API says true', () => {
    expect(answer({ success: true, message: '', data: true, errors: [] })).toBeTrue();
  });

  it('is false when the API says false', () => {
    expect(answer({ success: true, message: '', data: false, errors: [] })).toBeFalse();
  });

  it('is false for an empty or unexpected body', () => {
    expect(answer({ success: false, message: 'x', data: null, errors: [] })).toBeFalse();
    expect(answer({ success: true, data: { canReview: true } })).toBeFalse();   // not the API's shape
  });
});
