import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ClientsService, UpsertClientRequest } from '../../../../core/services/clients.service';

@Component({
  selector: 'app-client-edit-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './client-edit-page.html',
})
export class ClientEditPage {
  private readonly clientsService = inject(ClientsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;

  protected readonly name = signal('');
  protected readonly accountCode = signal('');
  protected readonly billingAddressLine1 = signal('');
  protected readonly billingCity = signal('');
  protected readonly billingPostalCode = signal('');
  protected readonly billingCountryCode = signal('');
  protected readonly primaryContactName = signal('');
  protected readonly primaryContactEmail = signal('');
  protected readonly reportingCurrencyCode = signal('GBP');
  protected readonly invoicingMonthEndDay = signal<number | null>(null);
  protected readonly notes = signal('');
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.editId) {
      this.clientsService.getById(Number(this.editId)).subscribe((c) => {
        this.name.set(c.name);
        this.accountCode.set(c.accountCode);
        this.billingAddressLine1.set(c.billingAddressLine1 ?? '');
        this.billingCity.set(c.billingCity ?? '');
        this.billingPostalCode.set(c.billingPostalCode ?? '');
        this.billingCountryCode.set(c.billingCountryCode ?? '');
        this.primaryContactName.set(c.primaryContactName ?? '');
        this.primaryContactEmail.set(c.primaryContactEmail ?? '');
        this.reportingCurrencyCode.set(c.reportingCurrencyCode);
        this.invoicingMonthEndDay.set(c.invoicingMonthEndDay);
        this.notes.set(c.notes ?? '');
      });
    }
  }

  protected save(): void {
    const request: UpsertClientRequest = {
      name: this.name(),
      accountCode: this.accountCode(),
      billingAddressLine1: this.billingAddressLine1() || null,
      billingAddressLine2: null,
      billingCity: this.billingCity() || null,
      billingPostalCode: this.billingPostalCode() || null,
      billingCountryCode: this.billingCountryCode() || null,
      primaryContactName: this.primaryContactName() || null,
      primaryContactEmail: this.primaryContactEmail() || null,
      primaryContactPhone: null,
      reportingCurrencyCode: this.reportingCurrencyCode().toUpperCase(),
      invoicingMonthEndDay: this.invoicingMonthEndDay(),
      notes: this.notes() || null,
    };

    const save$ = this.isEditMode
      ? this.clientsService.update(Number(this.editId), request)
      : this.clientsService.create(request);

    save$.subscribe({
      next: () => this.router.navigate(['/admin/clients']),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not save the client.'),
    });
  }

  protected cancel(): void {
    this.router.navigate(['/admin/clients']);
  }
}
