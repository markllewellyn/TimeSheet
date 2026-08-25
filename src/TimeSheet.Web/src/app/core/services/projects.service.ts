import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Project } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/projects`;

  /** The signed-in user's currently-assigned projects - the only projects they may log time/expenses against. */
  listAssignedToMe(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.baseUrl}/assigned-to-me`);
  }
}
