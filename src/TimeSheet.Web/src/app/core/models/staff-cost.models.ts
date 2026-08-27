// Mirrors TimeSheet.Contracts.StaffCostDto (C#) 1:1.

export interface StaffCost {
  id: number;
  staffId: number;
  staffName: string;
  hourlyCost: number;
  outOfHoursCost: number;
  effectiveFrom: string;
  createdUtc: string;
}
