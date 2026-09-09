import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Attachment } from '../models/timesheet-entry.models';
import { CreateExpenseEntryRequest, ExpenseEntry, UpdateExpenseEntryRequest } from '../models/expense-entry.models';

@Injectable({ providedIn: 'root' })
export class ExpenseEntriesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/expense-entries`;

  /// onBehalfOfUserId is honored server-side only for an Admin actively impersonating that user (see
  /// ExpenseEntriesFunctions.CheckOwnership) - mirrors TimesheetEntriesService's own identical pattern.
  getById(id: number, onBehalfOfUserId?: number | null): Observable<ExpenseEntry> {
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.get<ExpenseEntry>(`${this.baseUrl}/${id}`, { params });
  }

  list(search?: string, onBehalfOfUserId?: number | null): Observable<ExpenseEntry[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.get<ExpenseEntry[]>(this.baseUrl, { params });
  }

  create(request: CreateExpenseEntryRequest): Observable<ExpenseEntry> {
    return this.http.post<ExpenseEntry>(this.baseUrl, request);
  }

  update(id: number, request: UpdateExpenseEntryRequest): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number, onBehalfOfUserId?: number | null): Observable<void> {
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { params });
  }

  uploadAttachment(expenseEntryId: number, file: File, onBehalfOfUserId?: number | null): Observable<Attachment> {
    const formData = new FormData();
    formData.append('file', file);
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.post<Attachment>(`${this.baseUrl}/${expenseEntryId}/attachments`, formData, { params });
  }

  downloadAttachment(attachmentId: number, onBehalfOfUserId?: number | null): Observable<Blob> {
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.get(`${environment.apiBaseUrl}/expense-attachments/${attachmentId}`, { params, responseType: 'blob' });
  }

  deleteAttachment(attachmentId: number, onBehalfOfUserId?: number | null): Observable<void> {
    let params = new HttpParams();
    if (onBehalfOfUserId) params = params.set('onBehalfOfUserId', onBehalfOfUserId);
    return this.http.delete<void>(`${environment.apiBaseUrl}/expense-attachments/${attachmentId}`, { params });
  }
}
