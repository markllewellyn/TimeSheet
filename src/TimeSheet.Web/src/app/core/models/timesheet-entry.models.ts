// Mirrors TimeSheet.Contracts.TimesheetEntryDto / TimesheetEntrySummaryDto / AttachmentDto (C#) 1:1.

export type TimesheetEntryStatus = 'Normal' | 'PendingApproval' | 'Approved' | 'Declined';

export interface Attachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

export interface TimesheetEntry {
  id: number;
  projectId: number;
  projectName: string;
  clientId: number;
  clientName: string;
  date: string;
  workHours: number;
  outOfHoursHours: number;
  description: string | null;
  status: TimesheetEntryStatus;
  toPayroll: number;
  approvedPayroll: boolean;
  sentToPayroll: boolean;
  attachments: Attachment[];
}

export interface TimesheetEntrySummary {
  mostRecentDayHours: number;
  totalWorkHours: number;
  totalOutOfHoursHours: number;
  totalHours: number;
}

export interface TimesheetEntriesResponse {
  entries: TimesheetEntry[];
  summary: TimesheetEntrySummary;
}

export interface CreateTimesheetEntryRequest {
  projectId: number;
  date: string;
  workHours: number;
  outOfHoursHours: number;
  description: string | null;
  adminSendToPayroll?: boolean | null;
  onBehalfOfUserId?: number | null;
}

export type UpdateTimesheetEntryRequest = Omit<CreateTimesheetEntryRequest, 'projectId'>;
