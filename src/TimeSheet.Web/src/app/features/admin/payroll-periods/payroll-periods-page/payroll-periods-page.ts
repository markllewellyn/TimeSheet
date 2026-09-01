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

  constructor() {
    this.payrollPeriodsService.list().subscribe((periods) => this.periods.set(periods));
  }

  protected select(period: PayrollPeriod): void {
    this.selectedPeriodId.set(period.id);
    this.selectedPeriod.set(null);
    this.payrollPeriodsService.getById(period.id).subscribe((detail) => this.selectedPeriod.set(detail));
  }
}
