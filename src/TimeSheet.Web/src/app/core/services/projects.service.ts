import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Project } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/projects`;

  /** The signed-in user's currently-assigned projects, or (Admin-only) another user's when impersonating them
   * to log time on their behalf - the only projects that person may log time/expenses against. */
  listAssignedToMe(userId?: number): Observable<Project[]> {
    let params = new HttpParams();
    if (userId) params = params.set('userId', userId);
    return this.http.get<Project[]>(`${this.baseUrl}/assigned-to-me`, { params });
  }
}
