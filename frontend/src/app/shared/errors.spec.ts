import { HttpErrorResponse } from '@angular/common/http';
import { apiError } from './errors';

describe('apiError', () => {
  it('uses the first ProblemDetails validation message', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'Invalid', errors: { email: ['Email is invalid.'] } },
    });
    expect(apiError(error)).toBe('Email is invalid.');
  });

  it('reports an unreachable API clearly', () => {
    expect(apiError(new HttpErrorResponse({ status: 0 }))).toBe('Cannot reach the TourFlow API.');
  });
});
