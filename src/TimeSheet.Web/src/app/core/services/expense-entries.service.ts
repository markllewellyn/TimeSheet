import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateExpenseEntryRequest, ExpenseEntry, UpdateExpenseEntryRequest } from '../models/expense-entry.models';

@Injectable({ providedIn: 'root' })
export class ExpenseEntriesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/expense-entries`;

  list(search?: string): Observable<ExpenseEntry[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);
    return this.http.get<ExpenseEntry[]>(this.baseUrl, { params });
  }

  create(request: CreateExpenseEntryRequest): Observable<ExpenseEntry> {
    return this.http.post<ExpenseEntry>(this.baseUrl, request);
  }

  update(id: number, request: UpdateExpenseEntryRequest): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
