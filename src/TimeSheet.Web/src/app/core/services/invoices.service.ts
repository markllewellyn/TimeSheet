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
  grossAmount: number;
  discountPercent: number | null;
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

/// A project manager's own-project view of an invoice - lineItems and myTotalAmount are already filtered/summed
/// to just the projects they manage, never the invoice's full (possibly broader) total.
export interface ProjectManagerInvoice {
  id: number;
  clientId: number;
  clientName: string;
  periodStart: string;
  periodEnd: string;
  reportingCurrency: string;
  status: 'Draft' | 'Finalized';
  invoiceNumber: string | null;
  myTotalAmount: number;
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

  listForProjectManager(): Observable<ProjectManagerInvoice[]> {
    return this.http.get<ProjectManagerInvoice[]>(`${this.baseUrl}/invoices/for-project-manager`);
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

  applyLineItemDiscount(invoiceId: number, lineItemId: number, discountPercent: number | null): Observable<Invoice> {
    return this.http.put<Invoice>(`${this.baseUrl}/invoices/${invoiceId}/line-items/${lineItemId}/discount`, { discountPercent });
  }

  downloadPdf(invoiceId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
  }
}
