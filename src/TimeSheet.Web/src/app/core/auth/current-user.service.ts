import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { EventType } from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Me } from '../models/user.models';
import { ImpersonationService } from '../services/impersonation.service';
import { LocalAuthService } from './local-auth.service';

/**
 * The app's own /api/me profile (Role, display info) is the client-side authorization source of truth -
 * NOT Entra ID token claims. Route guards and <AuthorizeView>-style UI gating read the `me` signal here, not
 * idTokenClaims.roles. Works the same regardless of whether the caller signed in via Entra SSO or a local
 * account - the HTTP interceptors attach whichever bearer token applies.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly msal = inject(MsalService);
  private readonly broadcast = inject(MsalBroadcastService);
  private readonly localAuth = inject(LocalAuthService);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly impersonation = inject(ImpersonationService);

  private readonly meSignal = signal<Me | null>(null);
  readonly me = this.meSignal.asReadonly();
  readonly isAdmin = computed(() => this.meSignal()?.role === 'Admin');
  readonly isProjectManager = computed(() => this.meSignal()?.isProjectManager === true);
  readonly isSignedIn = computed(() => this.msal.instance.getAllAccounts().length > 0 || this.localAuth.isSignedIn());

  // True once a signed-in account has no matching app User row yet (a fresh database, or an account an Admin
  // hasn't invited) - see BootstrapFunctions/CurrentUserMiddleware for the one-time first-admin escape hatch.
  private readonly notProvisionedSignal = signal(false);
  readonly notProvisioned = this.notProvisionedSignal.asReadonly();

  constructor() {
    // Read login success from the broadcast stream (not synchronously after loginRedirect() resolves) -
    // idTokenClaims/account info can briefly come back undefined immediately after login otherwise. Also
    // navigates away from wherever the Entra redirect URI landed us (see msal.factories.ts -
    // navigateToLoginRequestUrl: false) - mirrors LoginPage.signInLocally()'s post-login navigation.
    this.broadcast.msalSubject$
      .pipe(filter((msg) => msg.eventType === EventType.LOGIN_SUCCESS))
      .subscribe(() => {
        this.loadMe();
        this.router.navigate(['/timesheet']);
      });

    if (this.isSignedIn()) {
      this.loadMe();
    }
  }

  loginWithMicrosoft(): void {
    this.msal.loginRedirect();
  }

  logout(): void {
    this.impersonation.stop();
    if (this.localAuth.isSignedIn()) {
      this.localAuth.logout();
      this.meSignal.set(null);
      this.notProvisionedSignal.set(false);
      return;
    }
    this.msal.logoutRedirect();
  }

  refreshMe(): void {
    this.loadMe();
  }

  private loadMe(): void {
    this.http.get<Me>(`${environment.apiBaseUrl}/me`).subscribe({
      next: (me) => {
        this.meSignal.set(me);
        this.notProvisionedSignal.set(false);
      },
      error: (err) => {
        if (err?.status === 403) this.notProvisionedSignal.set(true);
      },
    });
  }
}
