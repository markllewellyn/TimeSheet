import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaymentModel, Project, ProjectAttachment, ProjectEstimate, ProjectType } from '../models/project.models';

export interface CreateProjectRequest {
  clientId: number;
  name: string;
  code: string;
  description: string | null;
  paymentModel: PaymentModel;
  projectType: ProjectType;
  canInvoice: boolean | null;
  isCostExempt: boolean;
  currencyOverride: string | null;
  startDate: string;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  projectManagerUserId: number | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
  projectType: ProjectType;
  canInvoice: boolean | null;
  isCostExempt: boolean;
  currencyOverride: string | null;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
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

  listAll(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/projects`);
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

  getEstimate(id: number): Observable<ProjectEstimate> {
    return this.http.get<ProjectEstimate>(`${this.baseUrl}/projects/${id}/estimate`);
  }

  listAttachments(projectId: number): Observable<ProjectAttachment[]> {
    return this.http.get<ProjectAttachment[]>(`${this.baseUrl}/projects/${projectId}/attachments`);
  }

  uploadAttachment(projectId: number, file: File): Observable<ProjectAttachment> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ProjectAttachment>(`${this.baseUrl}/projects/${projectId}/attachments`, formData);
  }

  downloadAttachment(attachmentId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/project-attachments/${attachmentId}`, { responseType: 'blob' });
  }

  deleteAttachment(attachmentId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/project-attachments/${attachmentId}`);
  }
}
