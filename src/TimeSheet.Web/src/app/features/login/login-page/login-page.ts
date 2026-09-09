import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { LocalAuthService } from '../../../core/auth/local-auth.service';

const ERROR_MESSAGES: Record<string, string> = {
  state_mismatch: 'Sign-in with Microsoft timed out or was interrupted - please try again.',
  not_provisioned: "Your Microsoft account isn't set up in TimeSheet yet - ask an Admin to add you.",
  session_expired: 'Your session has expired - please sign in again.',
};

/**
 * Offers both sign-in paths: Entra SSO (the primary path) and a local username/password account - the latter
 * doubles as a fallback if Entra SSO is ever unreachable/misconfigured, and as the only path for people
 * without an Entra identity in this tenant. "Sign in with Microsoft" is a plain top-level navigation to the
 * Api's own Auth_EntraLogin endpoint (NOT an HttpClient call, which would follow the redirect chain in the
 * background instead of navigating the tab) - the Api handles the whole Entra round trip server-side and
 * redirects back to entra-complete-page on success, or here with ?error=... on failure.
 */
@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly currentUser = inject(CurrentUserService);
  private readonly localAuth = inject(LocalAuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(this.readErrorFromQuery());
  protected readonly working = signal(false);

  protected signInWithMicrosoft(): void {
    window.location.href = `${environment.apiBaseUrl}/auth/entra-login`;
  }

  protected signInLocally(): void {
    this.working.set(true);
    this.localAuth.login(this.email(), this.password()).subscribe({
      next: (result) => {
        this.localAuth.setSession(result.token, result.expiresAtUtc);
        this.currentUser.refreshMe();
        this.working.set(false);
        this.router.navigate(['/timesheet']);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(err?.error?.error ?? 'Could not sign in.');
      },
    });
  }

  private readErrorFromQuery(): string | null {
    const code = this.route.snapshot.queryParamMap.get('error');
    return code ? (ERROR_MESSAGES[code] ?? 'Could not sign in.') : null;
  }
}
