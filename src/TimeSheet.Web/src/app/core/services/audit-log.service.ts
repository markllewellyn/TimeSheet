import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AuditLogEntry {
  id: number;
  userId: number;
  userDisplayName: string;
  impersonatedUserId: number | null;
  impersonatedUserDisplayName: string | null;
  action: string;
  entityType: string;
  entityId: number | null;
  details: string | null;
  createdUtc: string;
}

export interface AuditLogFilter {
  userId?: number | null;
  from?: string | null;
  to?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/audit-log`;

  list(filter: AuditLogFilter = {}): Observable<AuditLogEntry[]> {
    let params = new HttpParams();
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<AuditLogEntry[]>(this.baseUrl, { params });
  }
}
