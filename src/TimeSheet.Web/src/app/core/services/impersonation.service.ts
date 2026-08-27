import { Injectable, signal } from '@angular/core';

export interface ImpersonatedUser {
  id: number;
  displayName: string;
}

/// Deliberately in-memory only (no localStorage) - impersonation is a visible, temporary admin action for the
/// current session, not something that should silently persist across a reload or a different device.
@Injectable({ providedIn: 'root' })
export class ImpersonationService {
  readonly actingAs = signal<ImpersonatedUser | null>(null);

  start(user: ImpersonatedUser): void {
    this.actingAs.set(user);
  }

  stop(): void {
    this.actingAs.set(null);
  }
}
