import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaymentModel, Project, ProjectAttachment, ProjectBreakdown, ProjectEstimate, ProjectStatus, ProjectType } from '../models/project.models';

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

  getStatus(id: number): Observable<ProjectStatus> {
    return this.http.get<ProjectStatus>(`${this.baseUrl}/projects/${id}/status`);
  }

  getBreakdown(id: number): Observable<ProjectBreakdown> {
    return this.http.get<ProjectBreakdown>(`${this.baseUrl}/projects/${id}/breakdown`);
  }

  /** FDD: a project's nominated PM can view it - powers the My Projects page. Empty for anyone who manages
   * nothing, same "backend scopes, route stays open to any signed-in user" pattern as My Invoices.
   * onBehalfOfUserId lets an Admin impersonating a PM see that PM's own managed project(s). */
  listManagedByMe(onBehalfOfUserId?: number | null): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/projects/managed-by-me`, {
      params: onBehalfOfUserId ? { onBehalfOfUserId } : {},
    });
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
