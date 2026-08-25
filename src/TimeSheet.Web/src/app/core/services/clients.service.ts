import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Client } from '../models/project.models';

export interface UpsertClientRequest {
  name: string;
  accountCode: string;
  billingAddressLine1: string | null;
  billingAddressLine2: string | null;
  billingCity: string | null;
  billingPostalCode: string | null;
  billingCountryCode: string | null;
  primaryContactName: string | null;
  primaryContactEmail: string | null;
  primaryContactPhone: string | null;
  reportingCurrencyCode: string;
  invoicingMonthEndDay: number | null;
  notes: string | null;
}

@Injectable({ providedIn: 'root' })
export class ClientsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/clients`;

  list(includeInactive = false): Observable<Client[]> {
    return this.http.get<Client[]>(this.baseUrl, { params: { includeInactive } });
  }

  getById(id: number): Observable<Client> {
    return this.http.get<Client>(`${this.baseUrl}/${id}`);
  }

  create(request: UpsertClientRequest): Observable<Client> {
    return this.http.post<Client>(this.baseUrl, request);
  }

  update(id: number, request: UpsertClientRequest): Observable<Client> {
    return this.http.put<Client>(`${this.baseUrl}/${id}`, request);
  }

  deactivate(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, {});
  }
}
