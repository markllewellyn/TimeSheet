import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../core/services/settings.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  private readonly settingsService = inject(SettingsService);

  protected readonly baseReportingCurrency = signal('');
  protected readonly defaultInvoiceMonthEndDay = signal(31);
  // FDD: "Project feedback notifications are raised at 50% and 75% of the time allotted to a
  // project... The percentages are held in settings and can be changed by management."
  protected readonly projectBudgetWarningThresholdPercent = signal(50);
  protected readonly projectBudgetAlertThresholdPercent = signal(75);

  protected readonly loaded = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly savedMessage = signal<string | null>(null);

  constructor() {
    this.settingsService.get().subscribe((s) => {
      this.baseReportingCurrency.set(s.baseReportingCurrency);
      this.defaultInvoiceMonthEndDay.set(s.defaultInvoiceMonthEndDay);
      this.projectBudgetWarningThresholdPercent.set(s.projectBudgetWarningThresholdPercent);
      this.projectBudgetAlertThresholdPercent.set(s.projectBudgetAlertThresholdPercent);
      this.loaded.set(true);
    });
  }

  protected save(): void {
    this.saving.set(true);
    this.savedMessage.set(null);
    this.settingsService
      .update({
        baseReportingCurrency: this.baseReportingCurrency(),
        defaultInvoiceMonthEndDay: this.defaultInvoiceMonthEndDay(),
        projectBudgetWarningThresholdPercent: this.projectBudgetWarningThresholdPercent(),
        projectBudgetAlertThresholdPercent: this.projectBudgetAlertThresholdPercent(),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.error.set(null);
          this.savedMessage.set('Settings saved.');
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? 'Could not save settings.');
        },
      });
  }
}
