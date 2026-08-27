import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApprovalEntry, PendingApprovalsResponse } from '../models/approval.models';

@Injectable({ providedIn: 'root' })
export class ApprovalsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/approvals`;

  pending(search?: string): Observable<PendingApprovalsResponse> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<PendingApprovalsResponse>(`${this.baseUrl}/pending`, { params });
  }

  approve(entryIds: number[], postingBatch: string): Observable<{ approved: number }> {
    return this.http.post<{ approved: number }>(`${this.baseUrl}/approve`, { entryIds, postingBatch });
  }

  readyForPayroll(): Observable<ApprovalEntry[]> {
    return this.http.get<ApprovalEntry[]>(`${this.baseUrl}/ready-for-payroll`);
  }

  sendToPayroll(entryIds: number[]): Observable<{ sent: number }> {
    return this.http.post<{ sent: number }>(`${this.baseUrl}/send-to-payroll`, { entryIds });
  }
}
