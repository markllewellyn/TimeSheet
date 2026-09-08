// Mirrors TimeSheet.Contracts.ExpenseEntryDto (C#) 1:1.

import { Attachment } from './timesheet-entry.models';

export type ExpenseEntryKind = 'Expense' | 'Contract';

export interface ExpenseEntry {
  id: number;
  projectId: number;
  projectName: string;
  clientId: number;
  clientName: string;
  date: string;
  amount: number;
  currency: string;
  description: string | null;
  isBillable: boolean;
  kind: ExpenseEntryKind;
  attachments: Attachment[];
}

export interface CreateExpenseEntryRequest {
  projectId: number;
  date: string;
  amount: number;
  currency: string;
  description: string | null;
  isBillable: boolean;
  kind?: ExpenseEntryKind;
}

export type UpdateExpenseEntryRequest = Omit<CreateExpenseEntryRequest, 'projectId'>;
