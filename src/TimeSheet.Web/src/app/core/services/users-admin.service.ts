import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppUser } from '../models/user.models';

export interface InviteUserRequest {
  entraObjectId: string;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  jobTitle: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  role: 'Admin' | 'User';
  jobTitle: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class UsersAdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  list(includeInactive = true): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl, { params: { includeInactive } });
  }

  invite(request: InviteUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.baseUrl, request);
  }

  update(id: number, request: UpdateUserRequest): Observable<AppUser> {
    return this.http.put<AppUser>(`${this.baseUrl}/${id}`, request);
  }
}
