import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { CurrentUserService } from '../../../core/auth/current-user.service';

/**
 * Shown to a signed-in Entra account with no matching app User row yet. Only ever succeeds once, for the very
 * first person to claim it - see BootstrapFunctions/CurrentUserMiddleware.
 */
@Component({
  selector: 'app-setup-page',
  standalone: true,
  templateUrl: './setup-page.html',
})
export class SetupPage {
  private readonly http = inject(HttpClient);
  private readonly currentUser = inject(CurrentUserService);
  private readonly router = inject(Router);

  protected readonly error = signal<string | null>(null);
  protected readonly working = signal(false);

  protected claimAdmin(): void {
    this.working.set(true);
    this.http.post(`${environment.apiBaseUrl}/bootstrap/first-admin`, {}).subscribe({
      next: () => {
        this.currentUser.refreshMe();
        this.router.navigate(['/timesheet']);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(err?.error?.error ?? 'Could not claim admin access - someone may have already set up this database.');
      },
    });
  }
}
