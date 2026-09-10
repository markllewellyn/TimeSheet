import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { LocalAuthService } from './local-auth.service';

/**
 * The inverse of authGuard - keeps a signed-in user off the login page itself, redirecting to /timesheet
 * instead. Without this, navigating straight to /login while already signed in rendered the login form
 * underneath the app shell's own always-visible, already-signed-in header at the same time.
 */
export const guestGuard: CanActivateFn = () => {
  const localAuth = inject(LocalAuthService);
  const router = inject(Router);

  return localAuth.isSignedIn() ? router.parseUrl('/timesheet') : true;
};
