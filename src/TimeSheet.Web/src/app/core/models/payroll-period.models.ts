// Mirrors TimeSheet.Contracts.PayrollPeriodDto / PayrollPeriodLineDto / PayrollPeriodDetailDto (C#) 1:1.

export interface PayrollPeriod {
  id: number;
  periodStart: string;
  periodEnd: string;
  generatedAtUtc: string;
  totalOutOfHoursHours: number;
  totalOutOfHoursPay: number;
  staffCount: number;
}

export interface PayrollPeriodLine {
  userId: number;
  staffName: string;
  outOfHoursHours: number;
  outOfHoursPay: number;
}

export interface PayrollPeriodDetail {
  id: number;
  periodStart: string;
  periodEnd: string;
  generatedAtUtc: string;
  totalOutOfHoursHours: number;
  totalOutOfHoursPay: number;
  lines: PayrollPeriodLine[];
}
