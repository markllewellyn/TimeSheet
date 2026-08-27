// Mirrors TimeSheet.Contracts.MyOverviewLineDto (C#) 1:1.

export interface MyOverviewLine {
  clientId: number;
  clientName: string;
  projectId: number;
  projectName: string;
  workHours: number;
  outOfHoursHours: number;
  toPayroll: number;
  sentToPayroll: boolean;
}
