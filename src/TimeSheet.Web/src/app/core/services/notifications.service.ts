import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface AppNotification {
  id: number;
  type: string;
  message: string;
  isRead: boolean;
  createdAtUtc: string;
  relatedProjectId: number | null;
  relatedTimesheetEntryId: number | null;
}

const POLL_INTERVAL_MS = 90_000;

/**
 * Polling, not push (SignalR) - see the plan's Notifications section. Polls on an interval, on tab focus, and
 * once immediately when constructed (i.e. right after login, since this is a root-provided singleton).
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/notifications`;

  private readonly unreadSignal = signal<AppNotification[]>([]);
  readonly unread = this.unreadSignal.asReadonly();

  constructor() {
    this.poll();
    setInterval(() => this.poll(), POLL_INTERVAL_MS);
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') this.poll();
    });
  }

  /** For callers that just took an action which might have created a notification for the current user (e.g.
   * an Admin using "Notify Staff" on their own entry) - the bell would otherwise only catch up on the next
   * 90s poll or tab-focus event. */
  refresh(): void {
    this.poll();
  }

  private poll(): void {
    this.http.get<AppNotification[]>(`${this.baseUrl}/unread`).subscribe({
      next: (notifications) => this.unreadSignal.set(notifications),
      error: () => {}, // e.g. not signed in yet - the next poll will pick it up once authenticated
    });
  }

  markRead(id: number): void {
    this.http.post(`${this.baseUrl}/${id}/read`, {}).subscribe(() => {
      this.unreadSignal.update((list) => list.filter((n) => n.id !== id));
    });
  }

  markAllRead(): void {
    this.http.post(`${this.baseUrl}/read-all`, {}).subscribe(() => this.unreadSignal.set([]));
  }
}
