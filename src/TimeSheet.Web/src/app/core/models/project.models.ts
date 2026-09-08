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
  projectManagerUserId: number | null;
  projectManagerName: string | null;
  // FDD: "The project list shows a count of how many staff are assigned to each project." Active assignments only.
  assignedStaffCount: number;
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
