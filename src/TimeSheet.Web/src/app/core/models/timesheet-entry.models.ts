// Mirrors TimeSheet.Contracts.TimesheetEntryDto / TimesheetEntrySummaryDto / AttachmentDto (C#) 1:1.

export interface Attachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

// FDD: "When an entry is invoiced the user can choose to add it to the current billing period or to the next
// billing period." Next holds an entry out of its own natural (date-based) period and counts it toward the
// immediately following period instead - see ITimesheetEntryRepository.GetCountedForInvoicingAsync (backend).
export type BillingPeriodChoice = 'Current' | 'Next';

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
  toPayroll: number;
  approvedPayroll: boolean;
  sentToPayroll: boolean;
  attachments: Attachment[];
  entryTypeId: number | null;
  entryTypeName: string | null;
  billingPeriodChoice: BillingPeriodChoice;
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
  entryTypeId?: number | null;
  billingPeriodChoice?: BillingPeriodChoice | null;
}

export type UpdateTimesheetEntryRequest = Omit<CreateTimesheetEntryRequest, 'projectId'>;
