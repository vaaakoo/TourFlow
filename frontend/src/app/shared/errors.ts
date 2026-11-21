import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../core/models';
export function apiError(error: unknown) {
  if (!(error instanceof HttpErrorResponse)) return 'Something went wrong. Please try again.';
  const problem = error.error as ProblemDetails | undefined;
  const validation = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
  return (
    validation ??
    problem?.title ??
    (error.status === 0 ? 'Cannot reach the TourFlow API.' : `Request failed (${error.status}).`)
  );
}
