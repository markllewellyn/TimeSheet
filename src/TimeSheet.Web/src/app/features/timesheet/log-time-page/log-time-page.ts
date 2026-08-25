import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, themeQuartz } from 'ag-grid-community';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { TimesheetEntry, TimesheetEntrySummary } from '../../../core/models/timesheet-entry.models';
import { EntryActionsCell, EntryActionsContext } from './entry-actions-cell';

@Component({
  selector: 'app-log-time-page',
  standalone: true,
  imports: [AgGridAngular],
  templateUrl: './log-time-page.html',
})
export class LogTimePage {
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly router = inject(Router);

  protected readonly theme = themeQuartz;
  protected readonly searchText = signal('');
  protected readonly summary = signal<TimesheetEntrySummary | null>(null);
  protected readonly rowData = signal<TimesheetEntry[]>([]);

  private readonly actionsContext: EntryActionsContext = {
    onEdit: (entry) => this.router.navigate(['/timesheet', entry.id, 'edit']),
    onDuplicate: (entry) => this.duplicate(entry),
    onDelete: (entry) => this.delete(entry),
  };

  protected readonly columnDefs: ColDef<TimesheetEntry>[] = [
    { headerName: 'Client', field: 'clientName', flex: 1 },
    { headerName: 'Project', field: 'projectName', flex: 1 },
    { headerName: 'Date', field: 'date', width: 120 },
    { headerName: 'W/H', field: 'workHours', width: 90, type: 'numericColumn' },
    { headerName: 'O/H', field: 'outOfHoursHours', width: 90, type: 'numericColumn' },
    {
      headerName: 'Total Hours',
      width: 110,
      type: 'numericColumn',
      valueGetter: (p) => (p.data ? p.data.workHours + p.data.outOfHoursHours : null),
    },
    { headerName: 'Description', field: 'description', flex: 2 },
    { headerName: 'Status', field: 'status', width: 130 },
    {
      headerName: '',
      width: 200,
      sortable: false,
      filter: false,
      cellRenderer: EntryActionsCell,
      cellRendererParams: this.actionsContext,
    },
  ];

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.timesheetEntries.list({ search: this.searchText() || undefined }).subscribe((res) => {
      this.rowData.set(res.entries);
      this.summary.set(res.summary);
    });
  }

  protected onSearchChange(value: string): void {
    this.searchText.set(value);
    this.refresh();
  }

  protected addEntry(): void {
    this.router.navigate(['/timesheet/new']);
  }

  protected addExpense(): void {
    this.router.navigate(['/expenses/new']);
  }

  private duplicate(entry: TimesheetEntry): void {
    this.timesheetEntries.duplicate(entry.id).subscribe(() => this.refresh());
  }

  private delete(entry: TimesheetEntry): void {
    if (!confirm(`Delete this ${entry.workHours + entry.outOfHoursHours}h entry on ${entry.date}?`)) return;
    this.timesheetEntries.delete(entry.id).subscribe(() => this.refresh());
  }
}
