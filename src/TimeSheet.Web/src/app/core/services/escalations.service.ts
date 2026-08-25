import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Escalation {
  id: number;
  timesheetEntryId: number;
  projectId: number;
  projectName: string;
  clientName: string;
  reason: string;
  budgetLimitAtTimeOfEntry: number;
  cumulativeValueAtTimeOfEntry: number;
  raisedAtUtc: string;
  decision: 'Pending' | 'Approved' | 'Declined';
  decidedByUserId: number | null;
  decidedAtUtc: string | null;
  decisionNotes: string | null;
}

@Injectable({ providedIn: 'root' })
export class EscalationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/escalations`;

  listPending(): Observable<Escalation[]> {
    return this.http.get<Escalation[]>(`${this.baseUrl}/pending`);
  }

  approve(id: number, notes: string | null): Observable<Escalation> {
    return this.http.post<Escalation>(`${this.baseUrl}/${id}/approve`, { notes });
  }

  decline(id: number, notes: string | null): Observable<Escalation> {
    return this.http.post<Escalation>(`${this.baseUrl}/${id}/decline`, { notes });
  }
}
