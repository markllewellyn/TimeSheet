import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Role {
  id: number;
  name: string;
  isActive: boolean;
}

export interface CreateRoleRequest {
  name: string;
}

export interface UpdateRoleRequest {
  name: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/roles`;

  list(includeInactive = false): Observable<Role[]> {
    return this.http.get<Role[]>(this.baseUrl, { params: { includeInactive } });
  }

  create(request: CreateRoleRequest): Observable<Role> {
    return this.http.post<Role>(this.baseUrl, request);
  }

  update(id: number, request: UpdateRoleRequest): Observable<Role> {
    return this.http.put<Role>(`${this.baseUrl}/${id}`, request);
  }
}
