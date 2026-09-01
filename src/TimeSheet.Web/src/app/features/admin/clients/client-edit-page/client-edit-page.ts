import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ClientsService, UpsertClientRequest } from '../../../../core/services/clients.service';
import { CurrenciesService, Currency } from '../../../../core/services/currencies.service';
import { RateCardsService, RateCard } from '../../../../core/services/rate-cards.service';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { AppUser } from '../../../../core/models/user.models';
import { BillingPeriod } from '../../../../core/models/project.models';

@Component({
  selector: 'app-client-edit-page',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './client-edit-page.html',
})
export class ClientEditPage {
  private readonly clientsService = inject(ClientsService);
  private readonly currenciesService = inject(CurrenciesService);
  private readonly rateCardsService = inject(RateCardsService);
  private readonly usersAdmin = inject(UsersAdminService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;

  protected readonly name = signal('');
  protected readonly accountCode = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly billingAddressLine1 = signal('');
  protected readonly billingCity = signal('');
  protected readonly billingPostalCode = signal('');
  protected readonly billingCountryCode = signal('');
  protected readonly primaryContactName = signal('');
  protected readonly primaryContactEmail = signal('');
  protected readonly currencyId = signal<number | null>(null);
  protected readonly invoicingMonthEndDay = signal<number | null>(null);
  protected readonly notes = signal('');
  protected readonly billingPeriod = signal<BillingPeriod>('OneOff');
  protected readonly currentPeriodStart = signal<string | null>(null);
  protected readonly currentPeriodEnd = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly currencies = signal<Currency[]>([]);

  // Person+client rate overrides for this client - every dated row ever added is shown (never edited/deleted),
  // so the list is its own history, newest first (see RateCardsService.list ordering).
  protected readonly rateCards = signal<RateCard[]>([]);
  protected readonly staff = signal<AppUser[]>([]);
  protected readonly newRateStaffId = signal<number | null>(null);
  protected readonly newRateAmount = signal<number | null>(null);
  protected readonly newRateEffectiveFrom = signal(new Date().toISOString().slice(0, 10));
  protected readonly rateCardError = signal<string | null>(null);
  protected readonly rateCardSaving = signal(false);

  constructor() {
    this.currenciesService.list().subscribe((c) => this.currencies.set(c));

    if (this.editId) {
      const id = Number(this.editId);
      this.clientsService.getById(id).subscribe((c) => {
        this.name.set(c.name);
        this.accountCode.set(c.accountCode);
        this.startDate.set(c.startDate);
        this.billingAddressLine1.set(c.billingAddressLine1 ?? '');
        this.billingCity.set(c.billingCity ?? '');
        this.billingPostalCode.set(c.billingPostalCode ?? '');
        this.billingCountryCode.set(c.billingCountryCode ?? '');
        this.primaryContactName.set(c.primaryContactName ?? '');
        this.primaryContactEmail.set(c.primaryContactEmail ?? '');
        this.currencyId.set(c.currencyId);
        this.invoicingMonthEndDay.set(c.invoicingMonthEndDay);
        this.notes.set(c.notes ?? '');
        this.billingPeriod.set(c.billingPeriod);
        this.currentPeriodStart.set(c.currentPeriodStart);
        this.currentPeriodEnd.set(c.currentPeriodEnd);
      });
      this.refreshRateCards(id);
      this.usersAdmin.list(false).subscribe((users) => this.staff.set(users));
    }
  }

  private refreshRateCards(clientId: number): void {
    this.rateCardsService.list({ clientId }).subscribe((cards) => this.rateCards.set(cards));
  }

  protected save(): void {
    const request: UpsertClientRequest = {
      name: this.name(),
      accountCode: this.accountCode(),
      startDate: this.startDate(),
      billingAddressLine1: this.billingAddressLine1() || null,
      billingAddressLine2: null,
      billingCity: this.billingCity() || null,
      billingPostalCode: this.billingPostalCode() || null,
      billingCountryCode: this.billingCountryCode() || null,
      primaryContactName: this.primaryContactName() || null,
      primaryContactEmail: this.primaryContactEmail() || null,
      primaryContactPhone: null,
      currencyId: this.currencyId(),
      invoicingMonthEndDay: this.invoicingMonthEndDay(),
      notes: this.notes() || null,
      billingPeriod: this.billingPeriod(),
      currentPeriodStart: this.billingPeriod() === 'Monthly' ? this.currentPeriodStart() : null,
      currentPeriodEnd: this.billingPeriod() === 'Monthly' ? this.currentPeriodEnd() : null,
    };

    const save$ = this.isEditMode
      ? this.clientsService.update(Number(this.editId), request)
      : this.clientsService.create(request);

    this.saving.set(true);
    save$.subscribe({
      next: () => this.router.navigate(['/admin/clients']),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not save the client.');
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/admin/clients']);
  }

  protected addRateCard(): void {
    if (!this.editId) return;
    const staffId = this.newRateStaffId();
    if (!staffId || this.newRateAmount() === null || !this.newRateEffectiveFrom()) {
      this.rateCardError.set('Please select a staff member, enter a rate and an effective date.');
      return;
    }

    this.rateCardSaving.set(true);
    this.rateCardsService
      .create({
        staffId,
        clientId: Number(this.editId),
        rate: this.newRateAmount()!,
        effectiveFrom: this.newRateEffectiveFrom(),
      })
      .subscribe({
        next: () => {
          this.rateCardSaving.set(false);
          this.rateCardError.set(null);
          this.newRateStaffId.set(null);
          this.newRateAmount.set(null);
          this.refreshRateCards(Number(this.editId));
        },
        error: (err) => {
          this.rateCardSaving.set(false);
          this.rateCardError.set(err?.error?.error ?? 'Could not add the rate.');
        },
      });
  }
}
