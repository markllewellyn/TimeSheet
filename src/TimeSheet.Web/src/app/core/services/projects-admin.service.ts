import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaymentModel, Project, ProjectType } from '../models/project.models';

export interface CreateProjectRequest {
  clientId: number;
  name: string;
  code: string;
  description: string | null;
  paymentModel: PaymentModel;
  projectType: ProjectType;
  canInvoice: boolean | null;
  currencyOverride: string | null;
  startDate: string;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  budgetAlertThresholdPercent: number;
  projectManagerUserId: number | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
  projectType: ProjectType;
  canInvoice: boolean | null;
  currencyOverride: string | null;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  budgetAlertThresholdPercent: number;
  isActive: boolean;
  projectManagerUserId: number | null;
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
}
