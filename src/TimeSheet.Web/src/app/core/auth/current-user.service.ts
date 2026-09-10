import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Me } from '../models/user.models';
import { ImpersonationService } from '../services/impersonation.service';
import { LocalAuthService } from './local-auth.service';

/**
 * The app's own /api/me profile (Role, display info) is the client-side authorization source of truth. Works
 * the same regardless of whether the caller signed in via Entra SSO or a local account - both land on the
 * exact same LocalAuthService session token (see EntraAuthFunctions/entra-complete-page for how an Entra
 * login arrives here), so there's only ever one session mechanism to reason about.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly localAuth = inject(LocalAuthService);
  private readonly http = inject(HttpClient);
  private readonly impersonation = inject(ImpersonationService);

  private readonly meSignal = signal<Me | null>(null);
  readonly me = this.meSignal.asReadonly();
  readonly isAdmin = computed(() => this.meSignal()?.role === 'Admin');
  readonly isProjectManager = computed(() => this.meSignal()?.isProjectManager === true);
  readonly isSignedIn = computed(() => this.localAuth.isSignedIn());

  // True once a signed-in account has no matching app User row yet (a fresh database, or an account an Admin
  // hasn't invited) - see BootstrapFunctions/CurrentUserMiddleware for the one-time first-admin escape hatch.
  private readonly notProvisionedSignal = signal(false);
  readonly notProvisioned = this.notProvisionedSignal.asReadonly();

  // Resolves once the initial /api/me fetch (if any) has settled - adminGuard awaits this before reading
  // isAdmin(), since a fresh page load (cold URL, bookmark, refresh) otherwise evaluates the route guard
  // before this async call returns, sending a genuine Admin back to /timesheet because isAdmin() was still
  // false at that instant. Resolves immediately if there's no session to load a profile for.
  private resolveMeLoaded!: () => void;
  readonly meLoaded: Promise<void> = new Promise((resolve) => (this.resolveMeLoaded = resolve));

  constructor() {
    if (this.isSignedIn()) {
      this.loadMe();
    } else {
      this.resolveMeLoaded();
    }
  }

  logout(): void {
    this.impersonation.stop();
    this.localAuth.logout();
    this.meSignal.set(null);
    this.notProvisionedSignal.set(false);
  }

  refreshMe(): void {
    this.loadMe();
  }

  private loadMe(): void {
    this.http.get<Me>(`${environment.apiBaseUrl}/me`).subscribe({
      next: (me) => {
        this.meSignal.set(me);
        this.notProvisionedSignal.set(false);
        this.resolveMeLoaded();
      },
      error: (err) => {
        if (err?.status === 403) this.notProvisionedSignal.set(true);
        this.resolveMeLoaded();
      },
    });
  }
}
