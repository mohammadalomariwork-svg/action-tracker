import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';

import { errorInterceptor } from './error.interceptor';
import { ToastService } from '../services/toast.service';
import { showErrorHere } from '../http/server-reason';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error', 'success', 'warning', 'info']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  /** One failing GET; `onError` plays the screen's error handler. */
  function failWith(status: number, body: object | null, onError: (err: HttpErrorResponse) => void = () => {}): void {
    http.get('/api/thing').subscribe({ error: onError });
    backend.expectOne('/api/thing').flush(body, { status, statusText: 'Failed' });
  }

  function toasts(): string[] {
    return toast.error.calls.allArgs().map(a => a[0]);
  }

  describe('the toast text', () => {
    const cases: [string, number, object | null, string[]][] = [
      ['409 with a reason', 409, { success: false, message: 'The update was already submitted.' }, ['The update was already submitted.']],
      ['409 without a body', 409, null, ['Unexpected error (409).']],
      ['409 with only a ProblemDetails detail', 409, { title: 'Conflict', detail: 'Raw exception text' }, ['Unexpected error (409).']],
      ['422 with an errors list', 422, { errors: ['First.', 'Second.'] }, ['First. Second.']],
      ['422 from the exception middleware', 422, { title: 'Unprocessable Entity', detail: 'Sequence contains no elements' }, ['Unexpected error (422).']],
      ['503 with a reason', 503, { success: false, message: 'Tags are not available yet.' }, ['Tags are not available yet.']],
      ['429, another uncommon status', 429, { message: 'Slow down.' }, ['Slow down.']],
      ['403 with a reason', 403, { success: false, message: 'You can only edit objectives in your unit.' }, ['You can only edit objectives in your unit.']],
      ['403 without a body (policy)', 403, null, ['Access denied.']],
      ['400 errors list, one toast each (unchanged)', 400, { errors: ['A.', 'B.'] }, ['A.', 'B.']],
      ['400 message (unchanged)', 400, { success: false, message: 'Bad name.' }, ['Bad name.']],
      ['400 detail (unchanged)', 400, { title: 'Bad Request', detail: 'Not a working day.' }, ['Not a working day.']],
      ['400 validation dictionary (unchanged)', 400, { errors: { Name: ['Required.'] } }, ['Invalid request.']],
      ['404 stays generic', 404, { success: false, message: 'Project not found.' }, ['Resource not found.']],
      ['500 stays generic', 500, { success: false, message: 'Boom.' }, ['Server error. Please try again.']],
      ['401 has no toast', 401, { message: 'Expired.' }, []],
    ];

    for (const [name, status, body, expected] of cases) {
      it(name, fakeAsync(() => {
        failWith(status, body);
        tick();
        expect(toasts()).toEqual(expected);
      }));
    }

    it('a network failure (status 0) has no toast', fakeAsync(() => {
      http.get('/api/thing').subscribe({ error: () => {} });
      backend.expectOne('/api/thing').error(new ProgressEvent('error'));
      tick();
      expect(toasts()).toEqual([]);
    }));
  });

  describe('showErrorHere', () => {
    it('waits one task before toasting', fakeAsync(() => {
      failWith(409, { message: 'Locked.' });
      expect(toasts()).toEqual([]);
      tick();
      expect(toasts()).toEqual(['Locked.']);
    }));

    it('keeps the toast away when the screen shows the reason', fakeAsync(() => {
      let shown: string | null = null;
      failWith(409, { message: 'Locked.' }, err => shown = showErrorHere(err));
      tick();
      expect(shown as string | null).toBe('Locked.');
      expect(toasts()).toEqual([]);
    }));

    it('also for a 400 (no more toast plus on-page message)', fakeAsync(() => {
      failWith(400, { errors: ['A.', 'B.'] }, err => showErrorHere(err));
      tick();
      expect(toasts()).toEqual([]);
    }));

    it('is still in time after an await', fakeAsync(() => {
      firstValueFrom(http.get('/api/thing')).catch(err => showErrorHere(err));
      backend.expectOne('/api/thing').flush({ message: 'Locked.' }, { status: 409, statusText: 'Conflict' });
      tick();
      expect(toasts()).toEqual([]);
    }));

    it('a screen that forgets it gets an extra toast, never a missing message', fakeAsync(() => {
      failWith(409, { message: 'Locked.' }, () => { /* shows "Could not save." on the page */ });
      tick();
      expect(toasts()).toEqual(['Locked.']);
    }));

    it('only silences the error it was given', fakeAsync(() => {
      failWith(409, { message: 'First.' }, err => showErrorHere(err));
      failWith(409, { message: 'Second.' });
      tick();
      expect(toasts()).toEqual(['Second.']);
    }));

    it('is too late after a timer (documented limit)', fakeAsync(() => {
      failWith(409, { message: 'Locked.' }, err => setTimeout(() => showErrorHere(err), 10));
      tick(20);
      expect(toasts()).toEqual(['Locked.']);
    }));
  });
});
