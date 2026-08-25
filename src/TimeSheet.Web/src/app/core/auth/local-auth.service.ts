import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

const TOKEN_STORAGE_KEY = 'localAuthToken';
const EXPIRY_STORAGE_KEY = 'localAuthTokenExpiry';

/**
 * Username/password sign-in for local accounts - a first-class account type for people without an Entra
 * identity, and a fallback path if Entra SSO is ever unreachable/misconfigured. Entirely independent of MSAL:
 * the token is a self-issued JWT (see LocalAuthService/AuthFunctions on the Api) stored in localStorage.
 */
@Injectable({ providedIn: 'root' })
export class LocalAuthService {
  private readonly http = inject(HttpClient);

  private readonly tokenSignal = signal<string | null>(this.readStoredToken());
  readonly isSignedIn = () => this.tokenSignal() !== null;

  token(): string | null {
    return this.tokenSignal();
  }

  login(email: string, password: string) {
    return this.http.post<{ token: string; expiresAtUtc: string }>(`${environment.apiBaseUrl}/auth/local-login`, {
      email,
      password,
    });
  }

  setSession(token: string, expiresAtUtc: string): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
    localStorage.setItem(EXPIRY_STORAGE_KEY, expiresAtUtc);
    this.tokenSignal.set(token);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(EXPIRY_STORAGE_KEY);
    this.tokenSignal.set(null);
  }

  private readStoredToken(): string | null {
    try {
      const token = localStorage.getItem(TOKEN_STORAGE_KEY);
      const expiry = localStorage.getItem(EXPIRY_STORAGE_KEY);
      if (!token || !expiry) return null;
      if (new Date(expiry).getTime() <= Date.now()) {
        localStorage.removeItem(TOKEN_STORAGE_KEY);
        localStorage.removeItem(EXPIRY_STORAGE_KEY);
        return null;
      }
      return token;
    } catch {
      return null; // e.g. a private-browsing context blocking storage access
    }
  }
}
