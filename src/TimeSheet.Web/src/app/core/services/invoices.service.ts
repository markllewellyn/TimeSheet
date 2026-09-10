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
  // One line per TimesheetEntry/ExpenseEntry now (FDD: a line shows the staff member's name, the task date,
  // the description and the rate) - null for a Fixed Fee line, which has no natural per-entry shape.
  staffId: number | null;
  staffName: string | null;
  taskDate: string | null;
  rate: number | null;
}

export interface Invoice {
  id: number;
  clientId: number;
  clientName: string;
  periodStart: string;
  periodEnd: string;
  reportingCurrency: string;
  exchangeRate: number | null;
  status: 'Draft' | 'Finalized' | 'Voided';
  invoiceNumber: string | null;
  totalAmount: number;
  generatedAtUtc: string;
  finalizedAtUtc: string | null;
  lineItems: InvoiceLineItem[];
  // Set only once Status becomes 'Voided' - a permanent record of what happened and why, kept alongside the
  // still-downloadable PDF rather than deleting the invoice outright.
  voidedAtUtc: string | null;
  voidedByName: string | null;
  voidReason: string | null;
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
  status: 'Draft' | 'Finalized' | 'Voided';
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

  /** Bulk sibling of applyLineItemDiscount - applies one discount to every line for a project on this invoice
   * at once, since generation now produces one line per entry instead of one per project. */
  applyProjectDiscount(invoiceId: number, projectId: number, discountPercent: number | null): Observable<Invoice> {
    return this.http.put<Invoice>(`${this.baseUrl}/invoices/${invoiceId}/projects/${projectId}/discount`, { discountPercent });
  }

  downloadPdf(invoiceId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
  }

  /** Nothing was ever locked for a Draft, so this is a plain delete - no entries to unwind. */
  deleteDraft(invoiceId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/invoices/${invoiceId}`);
  }

  /** Keeps the invoice, its real number and its PDF permanently - unlocks every entry/expense that was locked
   * to it so they become invoiceable again. */
  voidInvoice(invoiceId: number, reason: string | null): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.baseUrl}/invoices/${invoiceId}/void`, { reason });
  }
}
