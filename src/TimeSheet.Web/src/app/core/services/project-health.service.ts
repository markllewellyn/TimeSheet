import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ProjectHealthStatus = 'OnTrack' | 'AtRisk' | 'Behind';

export interface ProjectHealthAssessment {
  id: number;
  projectId: number;
  projectName: string;
  clientName: string;
  assessedAtUtc: string;
  status: ProjectHealthStatus;
  summary: string;
  contributingFactors: string[];
  recommendedAction: string | null;
  percentBudgetConsumed: number | null;
  percentTimeElapsed: number | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectHealthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getDashboard(): Observable<ProjectHealthAssessment[]> {
    return this.http.get<ProjectHealthAssessment[]>(`${this.baseUrl}/projects/health/dashboard`);
  }

  reassess(projectId: number): Observable<ProjectHealthAssessment> {
    return this.http.post<ProjectHealthAssessment>(`${this.baseUrl}/projects/${projectId}/health/reassess`, {});
  }
}
