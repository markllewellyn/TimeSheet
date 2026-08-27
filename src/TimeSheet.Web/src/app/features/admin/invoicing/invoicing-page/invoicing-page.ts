import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ClientsService } from '../../../../core/services/clients.service';
import { Invoice, InvoicesService } from '../../../../core/services/invoices.service';
import { Client } from '../../../../core/models/project.models';

@Component({
  selector: 'app-invoicing-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './invoicing-page.html',
})
export class InvoicingPage {
  private readonly clientsService = inject(ClientsService);
  private readonly invoicesService = inject(InvoicesService);

  protected readonly clients = signal<Client[]>([]);
  protected readonly selectedClientId = signal<number | null>(null);
  protected readonly invoices = signal<Invoice[]>([]);

  protected readonly periodStart = signal(this.firstOfMonth());
  protected readonly periodEnd = signal(new Date().toISOString().slice(0, 10));
  protected readonly invoiceNumberDraft = signal('');
  protected readonly manualExchangeRateInput = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly sortNewestFirst = signal(true);

  protected readonly selectedClient = computed(() => this.clients().find((c) => c.id === this.selectedClientId()) ?? null);
  protected readonly requiresExchangeRate = computed(() => (this.selectedClient()?.reportingCurrencyCode ?? 'GBP').toUpperCase() !== 'GBP');
  protected readonly sortedInvoices = computed(() => {
    const sorted = [...this.invoices()].sort((a, b) => a.generatedAtUtc.localeCompare(b.generatedAtUtc));
    return this.sortNewestFirst() ? sorted.reverse() : sorted;
  });

  constructor() {
    this.clientsService.list().subscribe((clients) => this.clients.set(clients));
  }

  private firstOfMonth(): string {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
  }

  protected onClientChange(clientId: number): void {
    this.selectedClientId.set(clientId);
    this.manualExchangeRateInput.set('');
    this.error.set(null);
    this.successMessage.set(null);
    this.refresh(clientId);
  }

  private refresh(clientId: number): void {
    this.invoicesService.listByClient(clientId).subscribe((invoices) => this.invoices.set(invoices));
  }

  protected generateDraft(): void {
    const clientId = this.selectedClientId();
    if (!clientId) return;

    let manualExchangeRate: number | undefined;
    if (this.requiresExchangeRate()) {
      const parsed = Number(this.manualExchangeRateInput());
      if (!Number.isFinite(parsed) || parsed <= 0) {
        this.error.set('Enter a valid exchange rate before generating this invoice.');
        return;
      }
      manualExchangeRate = parsed;
    }

    this.invoicesService.generateDraft(clientId, this.periodStart(), this.periodEnd(), manualExchangeRate).subscribe({
      next: () => {
        this.error.set(null);
        this.successMessage.set('Draft invoice generated.');
        this.refresh(clientId);
      },
      error: (err) => {
        this.successMessage.set(null);
        this.error.set(err?.error?.error ?? 'Could not generate the draft invoice.');
      },
    });
  }

  protected finalize(invoice: Invoice): void {
    if (!this.invoiceNumberDraft().trim()) {
      this.error.set('Enter an invoice number before finalizing.');
      return;
    }
    this.invoicesService.finalize(invoice.id, this.invoiceNumberDraft().trim()).subscribe({
      next: () => {
        this.invoiceNumberDraft.set('');
        this.error.set(null);
        this.successMessage.set('Invoice finalized.');
        this.refresh(invoice.clientId);
      },
      error: (err) => {
        this.successMessage.set(null);
        this.error.set(err?.error?.error ?? 'Could not finalize the invoice.');
      },
    });
  }

  protected downloadPdf(invoice: Invoice): void {
    this.invoicesService.downloadPdf(invoice.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `invoice-${invoice.invoiceNumber ?? invoice.id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }
}
