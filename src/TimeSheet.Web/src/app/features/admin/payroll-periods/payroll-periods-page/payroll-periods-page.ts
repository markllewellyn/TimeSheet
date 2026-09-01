import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { PayrollPeriodsService } from '../../../../core/services/payroll-periods.service';
import { PayrollPeriod, PayrollPeriodDetail } from '../../../../core/models/payroll-period.models';

@Component({
  selector: 'app-payroll-periods-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  templateUrl: './payroll-periods-page.html',
})
export class PayrollPeriodsPage {
  private readonly payrollPeriodsService = inject(PayrollPeriodsService);

  protected readonly periods = signal<PayrollPeriod[]>([]);
  protected readonly selectedPeriod = signal<PayrollPeriodDetail | null>(null);
  protected readonly selectedPeriodId = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly running = signal(false);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.payrollPeriodsService.list().subscribe({
      next: (periods) => this.periods.set(periods),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not load payroll periods.'),
    });
  }

  protected select(period: PayrollPeriod): void {
    this.selectedPeriodId.set(period.id);
    this.selectedPeriod.set(null);
    this.payrollPeriodsService.getById(period.id).subscribe({
      next: (detail) => this.selectedPeriod.set(detail),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not load that payroll period.'),
    });
  }

  protected runNow(): void {
    this.running.set(true);
    this.payrollPeriodsService.runNow().subscribe({
      next: (period) => {
        this.running.set(false);
        this.error.set(null);
        this.successMessage.set(
          period.staffCount > 0
            ? `Generated ${this.monthLabel(period.periodStart)}: ${period.staffCount} staff, ${period.totalOutOfHoursHours}h.`
            : `Ran for ${this.monthLabel(period.periodStart)} - no approved-but-unsent out-of-hours entries were found.`,
        );
        this.refresh();
      },
      error: (err) => {
        this.running.set(false);
        this.successMessage.set(null);
        this.error.set(err?.error?.error ?? 'Could not run the payroll aggregation.');
      },
    });
  }

  private monthLabel(periodStart: string): string {
    return new Date(periodStart).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });
  }
}
