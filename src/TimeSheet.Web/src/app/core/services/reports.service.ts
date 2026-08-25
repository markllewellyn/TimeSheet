import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ReportType =
  | 'time-on-project' | 'time-on-client'
  | 'cost-on-project' | 'cost-on-client'
  | 'profit-on-project' | 'profit-on-client';

export type ReportRangePreset = 'Last7Days' | 'LastMonth' | 'LastYear' | 'Custom';

export interface ReportEnvelope {
  range: { start: string; end: string; preset: ReportRangePreset };
  currency: string;
  summary: Record<string, number>;
  breakdown: Record<string, unknown>[];
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/reports`;

  get(
    reportType: ReportType,
    scope: { clientId?: number; projectId?: number },
    range: { preset: ReportRangePreset; startDate?: string; endDate?: string },
  ): Observable<ReportEnvelope> {
    let params = new HttpParams().set('rangePreset', range.preset);
    if (scope.clientId) params = params.set('clientId', scope.clientId);
    if (scope.projectId) params = params.set('projectId', scope.projectId);
    if (range.preset === 'Custom') {
      params = params.set('startDate', range.startDate ?? '').set('endDate', range.endDate ?? '');
    }
    return this.http.get<ReportEnvelope>(`${this.baseUrl}/${reportType}`, { params });
  }
}
