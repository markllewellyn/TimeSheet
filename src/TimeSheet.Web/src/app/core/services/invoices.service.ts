import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface InvoiceLineItem {
  id: number;
  projectId: number;
  projectName: string;
  description: string;
  hours: number | null;
  amount: number;
  type: string;
}

export interface Invoice {
  id: number;
  clientId: number;
  clientName: string;
  periodStart: string;
  periodEnd: string;
  reportingCurrency: string;
  exchangeRate: number | null;
  status: 'Draft' | 'Finalized';
  invoiceNumber: string | null;
  totalAmount: number;
  generatedAtUtc: string;
  finalizedAtUtc: string | null;
  lineItems: InvoiceLineItem[];
}

@Injectable({ providedIn: 'root' })
export class InvoicesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  listByClient(clientId: number): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(`${this.baseUrl}/clients/${clientId}/invoices`);
  }

  generateDraft(clientId: number, periodStart: string, periodEnd: string, manualExchangeRate?: number): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.baseUrl}/clients/${clientId}/invoices/draft`, {
      periodStart,
      periodEnd,
      manualExchangeRate: manualExchangeRate ?? null,
    });
  }

  finalize(invoiceId: number, invoiceNumber: string): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.baseUrl}/invoices/${invoiceId}/finalize`, { invoiceNumber });
  }

  downloadPdf(invoiceId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
  }
}
