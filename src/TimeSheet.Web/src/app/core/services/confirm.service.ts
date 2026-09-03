import { Injectable, signal } from '@angular/core';

export interface ConfirmRequest {
  message: string;
  confirmLabel: string;
  destructive: boolean;
}

/**
 * Replaces the native browser confirm() dialog with the app's own styled UI - see ConfirmDialog, mounted once
 * at the app root (app.html) so any page can trigger it without its own modal markup. Promise-based rather than
 * Observable-based to keep call sites a plain `if (!(await confirm(...)))` next to the rest of their logic.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly requestSignal = signal<ConfirmRequest | null>(null);
  readonly request = this.requestSignal.asReadonly();

  private resolver: ((result: boolean) => void) | null = null;

  confirm(message: string, options?: { confirmLabel?: string; destructive?: boolean }): Promise<boolean> {
    // A second confirm() while one is already open would leak the first caller's promise - not expected to
    // happen in practice (nothing in this app fires two confirmations at once), so resolving the stale one
    // as cancelled is a safe, simple guard rather than silently overwriting it.
    this.resolver?.(false);

    this.requestSignal.set({
      message,
      confirmLabel: options?.confirmLabel ?? 'Confirm',
      destructive: options?.destructive ?? false,
    });
    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  respond(result: boolean): void {
    this.requestSignal.set(null);
    this.resolver?.(result);
    this.resolver = null;
  }
}
