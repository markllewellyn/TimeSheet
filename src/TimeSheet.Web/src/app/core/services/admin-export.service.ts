import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ExportTimesheetEntriesFilter {
  from?: string;
  to?: string;
  projectId?: number | null;
  // Admin-only - ignored server-side for a non-admin caller regardless of what's sent.
  clientId?: number | null;
  userId?: number | null;
  projectManagerUserId?: number | null;
}

@Injectable({ providedIn: 'root' })
export class AdminExportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/exports/timesheet-entries`;

  downloadTimesheetEntriesCsv(filter: ExportTimesheetEntriesFilter): Observable<Blob> {
    let params = new HttpParams();
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    if (filter.projectId) params = params.set('projectId', filter.projectId);
    if (filter.clientId) params = params.set('clientId', filter.clientId);
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.projectManagerUserId) params = params.set('projectManagerUserId', filter.projectManagerUserId);
    return this.http.get(this.baseUrl, { params, responseType: 'blob' });
  }
}
