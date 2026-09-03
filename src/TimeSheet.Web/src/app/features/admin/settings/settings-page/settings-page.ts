import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../core/services/settings.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
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
    this.settingsService.get().subscribe({
      next: (s) => {
        this.baseReportingCurrency.set(s.baseReportingCurrency);
        this.defaultInvoiceMonthEndDay.set(s.defaultInvoiceMonthEndDay);
        this.projectBudgetWarningThresholdPercent.set(s.projectBudgetWarningThresholdPercent);
        this.projectBudgetAlertThresholdPercent.set(s.projectBudgetAlertThresholdPercent);
        this.loaded.set(true);
      },
      // Deliberately does NOT set loaded true here - this is a form that can Save over whatever it's showing,
      // so surfacing blank/default field values as if they were real settings (and letting Save overwrite the
      // actual saved settings with them) would be actively harmful, unlike a list page silently showing empty.
      error: (err) => this.error.set(err?.error?.error ?? 'Could not load settings.'),
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
