import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RateCard {
  id: number;
  roleId: number | null;
  roleName: string | null;
  staffId: number | null;
  staffName: string | null;
  clientId: number | null;
  clientName: string | null;
  projectId: number | null;
  projectName: string | null;
  rate: number;
  effectiveFrom: string;
  createdUtc: string;
}

export interface RateCardFilter {
  staffId?: number;
  roleId?: number;
  clientId?: number;
  projectId?: number;
}

/// Exactly one of roleId/staffId must be set; when staffId is set, exactly one of clientId/projectId must be
/// set (never both, never neither) - see RateCard.cs for the 5-tier scope shape.
export interface CreateRateCardRequest {
  roleId?: number | null;
  staffId?: number | null;
  clientId?: number | null;
  projectId?: number | null;
  rate: number;
  effectiveFrom: string;
}

@Injectable({ providedIn: 'root' })
export class RateCardsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/rate-cards`;

  list(filter: RateCardFilter = {}): Observable<RateCard[]> {
    const params: Record<string, number> = {};
    if (filter.staffId !== undefined) params['staffId'] = filter.staffId;
    if (filter.roleId !== undefined) params['roleId'] = filter.roleId;
    if (filter.clientId !== undefined) params['clientId'] = filter.clientId;
    if (filter.projectId !== undefined) params['projectId'] = filter.projectId;
    return this.http.get<RateCard[]>(this.baseUrl, { params });
  }

  create(request: CreateRateCardRequest): Observable<RateCard> {
    return this.http.post<RateCard>(this.baseUrl, request);
  }
}
