// Mirrors TimeSheet.Contracts.ExpenseEntryDto (C#) 1:1.

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
}

export interface CreateExpenseEntryRequest {
  projectId: number;
  date: string;
  amount: number;
  currency: string;
  description: string | null;
  isBillable: boolean;
}

export type UpdateExpenseEntryRequest = Omit<CreateExpenseEntryRequest, 'projectId'>;
