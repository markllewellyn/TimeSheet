import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LocalAuthService } from './local-auth.service';

/**
 * Attaches the local-account bearer token, when one exists, ahead of MsalInterceptor in the provider order -
 * a user is signed in via exactly one path (MSAL or local) at a time, so there's no risk of the two
 * conflicting. When there's no local session, this is a no-op and MsalInterceptor handles the request as usual.
 */
export const localAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const localAuth = inject(LocalAuthService);
  const token = localAuth.token();

  if (!token) return next(req);

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
