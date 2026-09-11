import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Team {
  id: number;
  name: string;
  isActive: boolean;
}

export interface CreateTeamRequest {
  name: string;
}

export interface UpdateTeamRequest {
  name: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class TeamsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/teams`;

  list(includeInactive = false): Observable<Team[]> {
    return this.http.get<Team[]>(this.baseUrl, { params: { includeInactive } });
  }

  create(request: CreateTeamRequest): Observable<Team> {
    return this.http.post<Team>(this.baseUrl, request);
  }

  update(id: number, request: UpdateTeamRequest): Observable<Team> {
    return this.http.put<Team>(`${this.baseUrl}/${id}`, request);
  }
}
