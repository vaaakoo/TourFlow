import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken;
  const outgoing = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;
  return next(outgoing).pipe(
    catchError((error: HttpErrorResponse) => {
      const authCall = ['/api/auth/login', '/api/auth/refresh', '/api/auth/register'].some((path) =>
        request.url.includes(path),
      );
      if (error.status !== 401 || authCall || !token) return throwError(() => error);
      return auth.refresh().pipe(
        switchMap(() =>
          next(request.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken}` } })),
        ),
        catchError((refreshError) => {
          auth.clear();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
