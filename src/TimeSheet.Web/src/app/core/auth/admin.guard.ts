import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CurrentUserService } from './current-user.service';

/**
 * Client-side gating only, for UX (hiding/redirecting away from admin screens) - the real security boundary is
 * the Api's own [AdminOnly]-equivalent check against User.Role (see AuthorizationExtensions.RequireAdmin in
 * TimeSheet.Api). Never rely on this guard alone.
 */
export const adminGuard: CanActivateFn = async () => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);
  await currentUser.meLoaded;
  return currentUser.isAdmin() ? true : router.parseUrl('/');
};
