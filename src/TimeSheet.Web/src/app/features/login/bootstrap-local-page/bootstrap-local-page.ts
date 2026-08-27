import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { LocalAuthService } from '../../../core/auth/local-auth.service';
import { CurrentUserService } from '../../../core/auth/current-user.service';

/**
 * Fully public (no sign-in required to reach it) - creates the very first Admin as a local account, no Entra
 * needed at all. Only ever succeeds once, for the very first person to claim it - see
 * BootstrapFunctions.FirstLocalAdmin/CurrentUserMiddleware.
 */
@Component({
  selector: 'app-bootstrap-local-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './bootstrap-local-page.html',
})
export class BootstrapLocalPage {
  private readonly http = inject(HttpClient);
  private readonly localAuth = inject(LocalAuthService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly displayName = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly working = signal(false);

  protected create(): void {
    this.working.set(true);
    this.http.post(`${environment.apiBaseUrl}/bootstrap/first-local-admin`, {
      email: this.email(),
      password: this.password(),
      displayName: this.displayName(),
    }).subscribe({
      next: () => {
        // Straight into a session rather than sending them back to /login to type it all in again.
        this.localAuth.login(this.email(), this.password()).subscribe({
          next: (result) => {
            this.localAuth.setSession(result.token, result.expiresAtUtc);
            this.currentUser.refreshMe();
            this.router.navigate(['/timesheet']);
          },
          error: () => this.router.navigate(['/login']),
        });
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(err?.error?.error ?? 'Could not create the admin account - someone may have already set up this database.');
      },
    });
  }
}
