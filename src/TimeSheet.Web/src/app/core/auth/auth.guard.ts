import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { LocalAuthService } from './local-auth.service';

/**
 * Replaces MsalGuard across the app's routes: lets a caller through if they're signed in via EITHER path
 * (Entra SSO or a local account), rather than forcing everyone through an Entra redirect. Unauthenticated
 * callers land on /login, which offers both options.
 */
export const authGuard: CanActivateFn = () => {
  const msal = inject(MsalService);
  const localAuth = inject(LocalAuthService);
  const router = inject(Router);

  if (msal.instance.getAllAccounts().length > 0 || localAuth.isSignedIn()) {
    return true;
  }

  return router.parseUrl('/login');
};
