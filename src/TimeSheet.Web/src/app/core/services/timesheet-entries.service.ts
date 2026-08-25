import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateTimesheetEntryRequest,
  TimesheetEntriesResponse,
  TimesheetEntry,
  UpdateTimesheetEntryRequest,
} from '../models/timesheet-entry.models';

export interface TimesheetEntriesFilter {
  search?: string;
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class TimesheetEntriesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/timesheet-entries`;

  getById(id: number): Observable<TimesheetEntry> {
    return this.http.get<TimesheetEntry>(`${this.baseUrl}/${id}`);
  }

  list(filter: TimesheetEntriesFilter = {}): Observable<TimesheetEntriesResponse> {
    let params = new HttpParams();
    if (filter.search) params = params.set('search', filter.search);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<TimesheetEntriesResponse>(this.baseUrl, { params });
  }

  create(request: CreateTimesheetEntryRequest): Observable<TimesheetEntry> {
    return this.http.post<TimesheetEntry>(this.baseUrl, request);
  }

  update(id: number, request: UpdateTimesheetEntryRequest): Observable<TimesheetEntry> {
    return this.http.put<TimesheetEntry>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  duplicate(id: number, date?: string): Observable<TimesheetEntry> {
    return this.http.post<TimesheetEntry>(`${this.baseUrl}/${id}/duplicate`, { date: date ?? null });
  }

  uploadAttachment(entryId: number, file: File): Observable<unknown> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post(`${this.baseUrl}/${entryId}/attachments`, formData);
  }
}
