import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { InvoicesService, ProjectManagerInvoice } from '../../../core/services/invoices.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';
import { groupInvoiceLineItemsByProject, InvoiceLineItemGroup } from '../../../core/utils/invoice-line-grouping';

/**
 * FDD: "Project managers can view the staged invoice for their projects." Read-only - a PM never generates,
 * amends or finalizes an invoice, so this deliberately has none of admin/invoicing's write actions. Shows
 * nothing but line items already scoped server-side to the caller's own managed project(s) - see
 * Invoices_ListForProjectManager.
 */
@Component({
  selector: 'app-my-invoices-page',
  standalone: true,
  imports: [DecimalPipe, LoadingSpinner],
  templateUrl: './my-invoices-page.html',
})
export class MyInvoicesPage {
  private readonly invoicesService = inject(InvoicesService);

  protected readonly invoices = signal<ProjectManagerInvoice[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.invoicesService.listForProjectManager().subscribe({
      next: (invoices) => {
        this.invoices.set(invoices);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load your invoices.');
      },
    });
  }

  protected groupedLineItems(invoice: ProjectManagerInvoice): InvoiceLineItemGroup[] {
    return groupInvoiceLineItemsByProject(invoice.lineItems);
  }

  protected statusBadgeClass(status: ProjectManagerInvoice['status']): string {
    switch (status) {
      case 'Finalized':
        return 'badge-success';
      case 'Voided':
        return 'badge-danger';
      default:
        return 'badge-warning';
    }
  }
}
