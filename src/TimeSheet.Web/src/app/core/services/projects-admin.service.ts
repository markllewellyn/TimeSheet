import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaymentModel, Project } from '../models/project.models';

export interface CreateProjectRequest {
  clientId: number;
  name: string;
  code: string;
  description: string | null;
  paymentModel: PaymentModel;
  currencyOverride: string | null;
  startDate: string;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  budgetAlertThresholdPercent: number;
  defaultCostRatePerHour: number;
  defaultBillingRatePerHour: number | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
  currencyOverride: string | null;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  budgetAlertThresholdPercent: number;
  isActive: boolean;
}

export interface ProjectRate {
  id: number;
  projectId: number;
  userId: number | null;
  userName: string | null;
  billingRatePerHour: number | null;
  costRatePerHour: number;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface UpsertProjectRateRequest {
  userId: number | null;
  billingRatePerHour: number | null;
  costRatePerHour: number;
  effectiveFrom: string;
  effectiveTo: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectsAdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listByClient(clientId: number, includeInactive = true): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/clients/${clientId}/projects`, { params: { includeInactive } });
  }

  getById(id: number): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`);
  }

  create(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(`${this.baseUrl}/projects`, request);
  }

  update(id: number, request: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/projects/${id}`, request);
  }

  listRates(projectId: number): Observable<ProjectRate[]> {
    return this.http.get<ProjectRate[]>(`${this.baseUrl}/projects/${projectId}/rates`);
  }

  createRate(projectId: number, request: UpsertProjectRateRequest): Observable<ProjectRate> {
    return this.http.post<ProjectRate>(`${this.baseUrl}/projects/${projectId}/rates`, request);
  }
}
