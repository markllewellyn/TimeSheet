import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { LocalAuthService } from '../../../core/auth/local-auth.service';

/**
 * Offers both sign-in paths: Entra SSO (the primary path) and a local username/password account - the latter
 * doubles as a fallback if Entra SSO is ever unreachable/misconfigured, and as the only path for people
 * without an Entra identity in this tenant.
 */
@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly currentUser = inject(CurrentUserService);
  private readonly localAuth = inject(LocalAuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly working = signal(false);

  protected signInWithMicrosoft(): void {
    this.currentUser.loginWithMicrosoft();
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
}
