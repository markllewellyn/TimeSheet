import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { LocalAuthService } from '../../../core/auth/local-auth.service';

/**
 * Landing point for a successful Entra sign-in (EntraAuthFunctions.EntraCallback redirects here on success).
 * The session token rides in the URL fragment - never sent to/logged by any server - rather than a query
 * param. Mirrors LoginPage.signInLocally()'s success path exactly: store the session, load /api/me, navigate
 * to /timesheet. Fully public (no auth guard) - reaching this page IS how a session gets established.
 */
@Component({
  selector: 'app-entra-complete-page',
  standalone: true,
  imports: [LoadingSpinner],
  templateUrl: './entra-complete-page.html',
})
export class EntraCompletePage {
  private readonly localAuth = inject(LocalAuthService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly router = inject(Router);

  constructor() {
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const token = params.get('token');
    const expiresAtUtc = params.get('expiresAtUtc');

    if (token && expiresAtUtc) {
      this.localAuth.setSession(token, expiresAtUtc);
      this.currentUser.refreshMe();
      this.router.navigate(['/timesheet']);
    } else {
      this.router.navigate(['/login'], { queryParams: { error: 'state_mismatch' } });
    }
  }
}
