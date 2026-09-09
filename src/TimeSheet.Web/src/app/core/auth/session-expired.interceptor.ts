import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CurrentUserService } from './current-user.service';

/**
 * A 401 from any authenticated API call means the token this app is holding is no longer valid server-side -
 * expired, or (in local dev) signed with a key that's since changed, e.g. the API host restarted with a freshly
 * generated ephemeral LocalAuth:JwtSigningKey (see Program.cs's own comment on that). LocalAuthService's own
 * expiry check can't catch this: it only compares the client-stored expiry timestamp against the clock, so a
 * token that's invalid for any other reason still looks "signed in" locally. Without this, the app is left
 * showing a half-signed-in UI (nav bar still shows the signed-in header, but every page's own API calls fail
 * with their own generic "could not load" error) instead of cleanly dropping back to the login screen.
 * Excludes the login endpoint itself - a 401 from there means "wrong password", not "your session expired".
 */
export const sessionExpiredInterceptor: HttpInterceptorFn = (req, next) => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401 && !req.url.endsWith('/auth/local-login') && currentUser.isSignedIn()) {
        currentUser.logout();
        router.navigate(['/login'], { queryParams: { error: 'session_expired' } });
      }
      return throwError(() => err);
    }),
  );
};
