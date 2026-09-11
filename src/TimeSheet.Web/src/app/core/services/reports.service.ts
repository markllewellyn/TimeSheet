import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ReportType =
  | 'time-on-project' | 'time-on-client'
  | 'cost-on-project' | 'cost-on-client'
  | 'profit-on-project' | 'profit-on-client'
  // FDD: "...on a team, role and user basis." Role/Team-basis only exists for Project scope - see
  // IReportingService's doc comment (Client scope can't honestly attribute Fixed Fee revenue recognition to a
  // specific role or team).
  | 'time-on-project-by-role' | 'cost-on-project-by-role' | 'profit-on-project-by-role'
  | 'time-on-project-by-team' | 'cost-on-project-by-team' | 'profit-on-project-by-team';

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
