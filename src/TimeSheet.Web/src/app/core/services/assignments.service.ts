import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type AssignmentStatus = 'Active' | 'Paused' | 'Ended';

export interface ProjectAssignment {
  id: number;
  projectId: number;
  projectName: string;
  userId: number;
  userDisplayName: string;
  status: AssignmentStatus;
  startDate: string;
  endDate: string | null;
  allocatedHoursPerWeek: number | null;
  notes: string | null;
}

export interface CreateProjectAssignmentRequest {
  projectId: number;
  userId: number;
  startDate: string;
  allocatedHoursPerWeek: number | null;
  notes: string | null;
}

export interface UpdateProjectAssignmentRequest {
  status: AssignmentStatus;
  endDate: string | null;
  allocatedHoursPerWeek: number | null;
  notes: string | null;
}

/// A staff member's assignment on one project, across ALL their projects (as opposed to ProjectAssignment,
/// which is scoped to one project's assignees) - carries a resolved-rate preview for the Staff screen.
export interface StaffAssignment {
  id: number;
  projectId: number;
  projectName: string;
  clientName: string;
  status: AssignmentStatus;
  startDate: string;
  endDate: string | null;
  allocatedHoursPerWeek: number | null;
  notes: string | null;
  resolvedCustomerRate: number | null;
  rateSource: string | null;
  rateWarning: string | null;
}

@Injectable({ providedIn: 'root' })
export class AssignmentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listByProject(projectId: number, activeOnly = false): Observable<ProjectAssignment[]> {
    return this.http.get<ProjectAssignment[]>(`${this.baseUrl}/projects/${projectId}/assignments`, { params: { activeOnly } });
  }

  listByUser(userId: number): Observable<StaffAssignment[]> {
    return this.http.get<StaffAssignment[]>(`${this.baseUrl}/users/${userId}/assignments`);
  }

  create(request: CreateProjectAssignmentRequest): Observable<ProjectAssignment> {
    return this.http.post<ProjectAssignment>(`${this.baseUrl}/assignments`, request);
  }

  update(id: number, request: UpdateProjectAssignmentRequest): Observable<ProjectAssignment> {
    return this.http.put<ProjectAssignment>(`${this.baseUrl}/assignments/${id}`, request);
  }
}
