import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthStore } from '../stores/auth.store';
import type { ProblemDetails } from '../models';

/**
 * Turns the API's RFC 7807 responses into plain Error objects carrying a message
 * fit to show a user, so no component has to know the wire format.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  return next(req).pipe(
    catchError((response: HttpErrorResponse) => {
      // 401 on anything other than the login call itself means the token expired
      // mid-session; drop it and send the user back to sign in.
      if (response.status === 401 && !req.url.includes('/auth/')) {
        auth.signOut();
        void router.navigate(['/login']);
      }

      return throwError(() => new Error(toMessage(response)));
    }),
  );
};

function toMessage(response: HttpErrorResponse): string {
  if (response.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }

  const problem = response.error as ProblemDetails | undefined;

  // Field-level validation errors are the most useful thing to show, so they win
  // over the generic title.
  if (problem?.errors) {
    const messages = Object.values(problem.errors).flat();
    if (messages.length > 0) {
      return messages.join('\n');
    }
  }

  if (problem?.detail) {
    return problem.detail;
  }

  return response.status >= 500
    ? 'Something went wrong on our end. Please try again.'
    : 'That request could not be completed.';
}
