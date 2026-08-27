// Mirrors TimeSheet.Contracts.ApprovalEntryDto / PendingApprovalsResponse (C#) 1:1.

export interface ApprovalEntry {
  id: number;
  staffId: number;
  staffName: string;
  date: string;
  description: string | null;
  projectId: number;
  projectName: string;
  clientId: number;
  clientName: string;
  workHours: number;
  outOfHoursHours: number;
  toPayroll: number;
  approvedPayroll: boolean;
  sentToPayroll: boolean;
  postingBatch: string | null;
}

export interface PendingApprovalsResponse {
  entries: ApprovalEntry[];
  postingBatches: string[];
}
