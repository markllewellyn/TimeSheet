// Mirrors TimeSheet.Contracts.TimesheetEntryDto / TimesheetEntrySummaryDto / AttachmentDto (C#) 1:1.

export interface Attachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

// Mirrors TimeSheet.Contracts.EntryFlagDto (C#) 1:1 - see also core/services/entry-flags.service.ts's EntryFlag,
// which duplicates this shape for the Entry Flags admin page's own use (kept separate from this models file
// deliberately - models here don't import from services).
export interface OpenEntryFlag {
  id: number;
  timesheetEntryId: number;
  projectId: number;
  projectName: string | null;
  clientName: string | null;
  reason: string;
  budgetLimitAtTimeOfEntry: number;
  cumulativeValueAtTimeOfEntry: number;
  raisedByUserId: number | null;
  raisedNotes: string | null;
  raisedAtUtc: string;
  isCleared: boolean;
  clearedByUserId: number | null;
  clearedAtUtc: string | null;
  clearedNotes: string | null;
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
  // FDD: "Finalizing an invoice locks the entries it was built from."
  invoiced: boolean;
  // A flag is never a gate (FDD) - this is purely informational, doesn't affect editability. Empty = not flagged.
  openFlags: OpenEntryFlag[];
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
