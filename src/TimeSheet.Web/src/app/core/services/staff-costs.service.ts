import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { StaffCost } from '../models/staff-cost.models';

export interface CreateStaffCostRequest {
  staffId: number;
  hourlyCost: number;
  outOfHoursCost: number;
  effectiveFrom: string;
}

@Injectable({ providedIn: 'root' })
export class StaffCostsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/staff-costs`;

  // Every dated row ever added for this person, newest first - its own history, since a "change" is always a
  // new row (see StaffCost.cs) rather than an edit.
  listByStaff(staffId: number): Observable<StaffCost[]> {
    return this.http.get<StaffCost[]>(this.baseUrl, { params: { staffId } });
  }

  create(request: CreateStaffCostRequest): Observable<StaffCost> {
    return this.http.post<StaffCost>(this.baseUrl, request);
  }
}
