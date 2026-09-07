import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { LocalAuthService } from './local-auth.service';

/**
 * Both Entra SSO and local-account sign-ins end up as the same kind of session token (see
 * EntraAuthFunctions/entra-complete-page for how an Entra login lands here) - LocalAuthService's session is
 * the one source of truth regardless of which path the caller signed in through.
 */
export const authGuard: CanActivateFn = () => {
  const localAuth = inject(LocalAuthService);
  const router = inject(Router);

  if (localAuth.isSignedIn()) {
    return true;
  }

  return router.parseUrl('/login');
};
