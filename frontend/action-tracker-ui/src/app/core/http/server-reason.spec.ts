import { HttpErrorResponse } from '@angular/common/http';
import { isShownHere, serverReason, showErrorHere } from './server-reason';

function fail(status: number, error: unknown): HttpErrorResponse {
  return new HttpErrorResponse({ status, error });
}

describe('serverReason', () => {
  it('reads the ApiResponse message first', () => {
    expect(serverReason(fail(409, { success: false, message: 'Locked.', errors: ['Other.'] }))).toBe('Locked.');
  });

  it('falls back to an errors list, joined', () => {
    expect(serverReason(fail(422, { errors: ['First.', 'Second.'] }))).toBe('First. Second.');
  });

  it('flattens the FluentValidation dictionary and drops repeats', () => {
    expect(serverReason(fail(400, { title: 'One or more validation errors occurred.',
      errors: { Name: ['Name is required.'], Code: ['Name is required.', 'Code is too long.'] } })))
      .toBe('Name is required. Code is too long.');
  });

  it('uses the ProblemDetails detail on a 400 only', () => {
    expect(serverReason(fail(400, { title: 'Bad Request', detail: '2026-11-28 is not a working day.' })))
      .toBe('2026-11-28 is not a working day.');
    expect(serverReason(fail(422, { title: 'Unprocessable Entity', detail: 'Raw exception text' }))).toBeNull();
    expect(serverReason(fail(500, { title: 'Internal Server Error', detail: 'SqlException …' }))).toBeNull();
  });

  it('is null when the reply carries no reason', () => {
    expect(serverReason(fail(409, null))).toBeNull();
    expect(serverReason(fail(409, { success: false, message: '' }))).toBeNull();
    expect(serverReason(fail(409, { message: '   ', errors: [] }))).toBeNull();
    expect(serverReason(fail(503, 'plain text body'))).toBeNull();
    expect(serverReason(fail(0, new ProgressEvent('error')))).toBeNull();
    expect(serverReason(undefined)).toBeNull();
    expect(serverReason(new Error('not HTTP'))).toBeNull();
  });
});

describe('showErrorHere', () => {
  it('returns the reason and marks that error only', () => {
    const shown = fail(409, { message: 'Locked.' });
    const other = fail(409, { message: 'Locked.' });

    expect(showErrorHere(shown)).toBe('Locked.');
    expect(isShownHere(shown)).toBeTrue();
    expect(isShownHere(other)).toBeFalse();
  });

  it('returns null without a reason but still marks the error', () => {
    const err = fail(503, null);
    expect(showErrorHere(err)).toBeNull();
    expect(isShownHere(err)).toBeTrue();
  });

  it('accepts anything a catch block can hold', () => {
    expect(showErrorHere(undefined)).toBeNull();
    expect(showErrorHere('text')).toBeNull();
    expect(isShownHere('text')).toBeFalse();
    expect(isShownHere(null)).toBeFalse();
  });
});
