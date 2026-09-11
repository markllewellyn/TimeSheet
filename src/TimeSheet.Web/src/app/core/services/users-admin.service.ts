import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppUser, TenantDirectoryUser } from '../models/user.models';

export interface InviteUserRequest {
  // Omit/leave blank for a local (username/password) account instead of an SSO account.
  entraObjectId: string | null;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
  jobRoleId: number | null;
  teamId: number | null;
  // Leave blank to auto-generate a placeholder - fill in when the real payroll number is known.
  payrollNumber?: string | null;
}

export interface InviteUserResponse {
  user: AppUser;
  // Only set for a local account - a one-time temporary password to hand to the person.
  temporaryPassword: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  role: 'Admin' | 'User';
  jobRoleId: number | null;
  teamId: number | null;
  isActive: boolean;
  payrollNumber: string;
}

@Injectable({ providedIn: 'root' })
export class UsersAdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/users`;

  list(includeInactive = true): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.baseUrl, { params: { includeInactive } });
  }

  invite(request: InviteUserRequest): Observable<InviteUserResponse> {
    return this.http.post<InviteUserResponse>(this.baseUrl, request);
  }

  update(id: number, request: UpdateUserRequest): Observable<AppUser> {
    return this.http.put<AppUser>(`${this.baseUrl}/${id}`, request);
  }

  // Branches server-side: Graph for an SSO account, a direct local reset otherwise. Returns a one-time
  // temporary password either way. Route deliberately avoids a leading "admin/" segment - Azure Functions
  // reserves that for its own host API and silently drops user-defined routes that start with it.
  resetPassword(id: number): Observable<{ temporaryPassword: string }> {
    return this.http.post<{ temporaryPassword: string }>(`${this.baseUrl}/${id}/reset-password`, {});
  }

  // Entra directory lookup so an admin can find someone to invite without already knowing their raw Entra
  // Object Id - see AdminUsers_SearchTenantDirectory. Requires at least 2 characters.
  searchTenantDirectory(query: string): Observable<TenantDirectoryUser[]> {
    return this.http.get<TenantDirectoryUser[]>(`${environment.apiBaseUrl}/tenant-directory/search`, { params: { q: query } });
  }
}
