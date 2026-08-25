import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { EventType } from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Me } from '../models/user.models';

/**
 * The app's own /api/me profile (Role, display info) is the client-side authorization source of truth -
 * NOT Entra ID token claims. Route guards and <AuthorizeView>-style UI gating read the `me` signal here, not
 * idTokenClaims.roles.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly msal = inject(MsalService);
  private readonly broadcast = inject(MsalBroadcastService);
  private readonly http = inject(HttpClient);

  private readonly meSignal = signal<Me | null>(null);
  readonly me = this.meSignal.asReadonly();
  readonly isAdmin = computed(() => this.meSignal()?.role === 'Admin');
  readonly isSignedIn = computed(() => this.msal.instance.getAllAccounts().length > 0);

  constructor() {
    // Read login success from the broadcast stream (not synchronously after loginRedirect() resolves) -
    // idTokenClaims/account info can briefly come back undefined immediately after login otherwise.
    this.broadcast.msalSubject$
      .pipe(filter((msg) => msg.eventType === EventType.LOGIN_SUCCESS))
      .subscribe(() => this.loadMe());

    if (this.isSignedIn()) {
      this.loadMe();
    }
  }

  login(): void {
    this.msal.loginRedirect();
  }

  logout(): void {
    this.msal.logoutRedirect();
  }

  private loadMe(): void {
    this.http.get<Me>(`${environment.apiBaseUrl}/me`).subscribe((me) => this.meSignal.set(me));
  }
}
