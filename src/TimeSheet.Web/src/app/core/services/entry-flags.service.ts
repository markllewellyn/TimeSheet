import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EntryFlagSearchResult {
  id: number;
  staffId: number;
  staffName: string;
  date: string;
  description: string;
  projectName: string;
  clientName: string;
  workHours: number;
  outOfHoursHours: number;
}

export interface EntryFlag {
  id: number;
  timesheetEntryId: number;
  projectId: number;
  projectName: string | null;
  clientName: string | null;
  reason: string;
  budgetLimitAtTimeOfEntry: number;
  cumulativeValueAtTimeOfEntry: number;
  raisedByUserId: number | null;
  raisedNotes: string | null;
  raisedAtUtc: string;
  isCleared: boolean;
  clearedByUserId: number | null;
  clearedAtUtc: string | null;
  clearedNotes: string | null;
}

@Injectable({ providedIn: 'root' })
export class EntryFlagsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/entry-flags`;

  listOpen(): Observable<EntryFlag[]> {
    return this.http.get<EntryFlag[]>(`${this.baseUrl}/open`);
  }

  searchEntries(search: string): Observable<EntryFlagSearchResult[]> {
    return this.http.get<EntryFlagSearchResult[]>(`${this.baseUrl}/search-entries`, { params: { search } });
  }

  raiseManual(timesheetEntryId: number, notes: string | null): Observable<EntryFlag> {
    return this.http.post<EntryFlag>(`${this.baseUrl}/raise`, { timesheetEntryId, notes });
  }

  clear(id: number, notes: string | null): Observable<EntryFlag> {
    return this.http.post<EntryFlag>(`${this.baseUrl}/${id}/clear`, { notes });
  }

  notifyStaff(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/notify-staff`, {});
  }
}
