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

  // Admin account maintenance against Entra ID itself (via Graph) - returns a one-time temporary password.
  // Route deliberately avoids a leading "admin/" segment - Azure Functions reserves that for its own host API
  // and silently drops user-defined routes that start with it.
  resetPassword(id: number): Observable<{ temporaryPassword: string }> {
    return this.http.post<{ temporaryPassword: string }>(`${this.baseUrl}/${id}/reset-password`, {});
  }
}
