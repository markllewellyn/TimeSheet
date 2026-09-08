import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ExpenseEntriesService } from '../../../core/services/expense-entries.service';
import { ExpenseEntry } from '../../../core/models/expense-entry.models';
import { ConfirmService } from '../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';

/**
 * FDD: "Staff members must be able to put lines regarding expenses and add relevant attachments." Previously
 * had no view/manage surface at all - `+ Expense` on the Log Time page only ever reached a create-only form,
 * with no way to see, edit, or attach a receipt to an expense once logged. This is the missing list, mirroring
 * log-time-page's own list+actions role for timesheet entries.
 */
@Component({
  selector: 'app-expenses-list-page',
  standalone: true,
  imports: [DecimalPipe, LoadingSpinner],
  templateUrl: './expenses-list-page.html',
})
export class ExpensesListPage {
  private readonly expenseEntries = inject(ExpenseEntriesService);
  private readonly confirmService = inject(ConfirmService);
  private readonly router = inject(Router);

  protected readonly entries = signal<ExpenseEntry[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.expenseEntries.list().subscribe({
      next: (entries) => {
        this.entries.set(entries);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load your expenses.');
      },
    });
  }

  protected addExpense(): void {
    this.router.navigate(['/expenses/new']);
  }

  protected edit(entry: ExpenseEntry): void {
    this.router.navigate(['/expenses', entry.id, 'edit']);
  }

  protected async delete(entry: ExpenseEntry): Promise<void> {
    const confirmed = await this.confirmService.confirm(
      `Delete this ${entry.amount} ${entry.currency} entry on ${entry.date}?`,
      { confirmLabel: 'Delete', destructive: true },
    );
    if (!confirmed) return;

    this.expenseEntries.delete(entry.id).subscribe({
      next: () => this.refresh(),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not delete the entry.'),
    });
  }
}
