// Mirrors TimeSheet.Contracts.ClientDto / ProjectDto (C#) 1:1.

export interface Client {
  id: number;
  name: string;
  accountCode: string;
  startDate: string;
  billingAddressLine1: string | null;
  billingAddressLine2: string | null;
  billingCity: string | null;
  billingPostalCode: string | null;
  billingCountryCode: string | null;
  primaryContactName: string | null;
  primaryContactEmail: string | null;
  primaryContactPhone: string | null;
  currencyId: number | null;
  reportingCurrencyCode: string;
  invoicingMonthEndDay: number | null;
  notes: string | null;
  isActive: boolean;
  billingPeriod: BillingPeriod;
  currentPeriodStart: string | null;
  currentPeriodEnd: string | null;
}

export type PaymentModel = 'TimeAndMaterials' | 'FixedProjectCost';
export type BillingPeriod = 'OneOff' | 'Monthly';
export type ProjectType = 'Development' | 'Support' | 'Contract';

export interface Project {
  id: number;
  clientId: number;
  clientName: string;
  name: string;
  code: string;
  description: string | null;
  paymentModel: PaymentModel;
  projectType: ProjectType;
  canInvoice: boolean | null;
  isCostExempt: boolean;
  currencyOverride: string | null;
  startDate: string;
  endDate: string | null;
  budgetHours: number | null;
  fixedFeeAmount: number | null;
  isActive: boolean;
  // The owning client's IsActive (null if not loaded) - Add Entry/Add Expense hide an inactive client's projects.
  clientIsActive?: boolean | null;
  projectManagerUserId: number | null;
  projectManagerName: string | null;
  // FDD: "The project list shows a count of how many staff are assigned to each project." Active assignments only.
  assignedStaffCount: number;
  // Only populated by list/detail responses an Admin or the project's own PM can reach (listAll, listByClient,
  // listManagedByMe) - undefined everywhere else (getById, create, update, the assigned-to-me picker), which a
  // regular assigned staff member can also reach and must not see project financials through.
  actualHours?: number;
  hoursUsedPercent?: number | null;
  actualCost?: number;
  costUsedPercent?: number | null;
}

// Mirrors TimeSheet.Contracts.ProjectStatusDto - the Project edit page's own detail-panel fetch, kept separate
// from the fields above (which only ever arrive bundled onto a Project from a list/managed-by-me response).
export interface ProjectStatus {
  projectId: number;
  actualHours: number;
  budgetHours: number | null;
  hoursUsedPercent: number | null;
  actualCost: number;
  fixedFeeAmount: number | null;
  costUsedPercent: number | null;
}

// Mirrors TimeSheet.Contracts.ProjectBreakdownDto - the project detail drill-down page's "who has done what,
// and on what" view (by staff, by entry type, by month).
export interface StaffBreakdownLine {
  userId: number;
  userName: string;
  hours: number;
  cost: number;
  revenue: number;
  profit: number;
}

export interface EntryTypeBreakdownLine {
  entryTypeId: number | null;
  entryTypeName: string;
  hours: number;
}

export interface MonthBreakdownLine {
  year: number;
  month: number;
  hours: number;
}

export interface ProjectBreakdown {
  projectId: number;
  byStaff: StaffBreakdownLine[];
  byEntryType: EntryTypeBreakdownLine[];
  byMonth: MonthBreakdownLine[];
  // Only non-null for a Fixed Project Cost project - see ProjectBreakdownService's server-side comment.
  recognizedRevenueToDate: number | null;
}

export interface ProjectEstimateLine {
  userId: number;
  userName: string;
  roleId: number | null;
  roleName: string | null;
  allocatedHours: number;
  hourlyCost: number | null;
  customerRate: number | null;
  estimatedCost: number;
  estimatedRevenue: number;
  estimatedProfit: number;
  warning: string | null;
}

export interface ProjectEstimate {
  projectId: number;
  budgetHours: number | null;
  estimatedCost: number;
  estimatedRevenue: number;
  estimatedProfit: number;
  lines: ProjectEstimateLine[];
}

// FDD: "Attachments and documents can be held against a project." Mirrors TimeSheet.Contracts.ProjectAttachmentDto.
export interface ProjectAttachment {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  uploadedByName: string;
}
