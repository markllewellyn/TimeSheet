import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, themeQuartz } from 'ag-grid-community';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { WorkloadService } from '../../../core/services/workload.service';
import { TimesheetEntry, TimesheetEntrySummary } from '../../../core/models/timesheet-entry.models';
import { EstimatedWeeklyWorkload } from '../../../core/models/workload.models';
import { EntryActionsCell, EntryActionsContext } from './entry-actions-cell';
import { ThemeService } from '../../../core/services/theme.service';
import { ImpersonationService } from '../../../core/services/impersonation.service';

const GRID_THEME_BASE = {
  borderRadius: 8,
  wrapperBorder: false,
  headerRowBorder: true,
  rowBorder: true,
  headerFontWeight: 600,
  fontFamily: { googleFont: 'Inter' },
} as const;

// Two complete theme objects, swapped wholesale via [theme] binding - more reliable than AG Grid's
// data-ag-theme-mode part-switching attribute, which didn't pick up the app's dark-mode toggle.
const LIGHT_GRID_THEME = themeQuartz.withParams({ ...GRID_THEME_BASE, accentColor: '#4F46E5' });
const DARK_GRID_THEME = themeQuartz.withParams({
  ...GRID_THEME_BASE,
  accentColor: '#818CF8',
  backgroundColor: '#232326',
  foregroundColor: '#fafafa',
  headerBackgroundColor: '#28282c',
  borderColor: 'rgba(255, 255, 255, 0.1)',
  chromeBackgroundColor: '#232326',
});

@Component({
  selector: 'app-log-time-page',
  standalone: true,
  imports: [AgGridAngular],
  templateUrl: './log-time-page.html',
})
export class LogTimePage {
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly workloadService = inject(WorkloadService);
  private readonly router = inject(Router);
  protected readonly themeService = inject(ThemeService);
  protected readonly impersonation = inject(ImpersonationService);

  // Mirrors the :root.dark tokens in styles.scss so the grid matches the rest of the app.
  protected readonly theme = computed(() => (this.themeService.mode() === 'dark' ? DARK_GRID_THEME : LIGHT_GRID_THEME));
  protected readonly searchText = signal('');
  protected readonly summary = signal<TimesheetEntrySummary | null>(null);
  protected readonly rowData = signal<TimesheetEntry[]>([]);
  protected readonly workload = signal<EstimatedWeeklyWorkload | null>(null);

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
    { headerName: 'To Payroll', field: 'toPayroll', width: 110, type: 'numericColumn', valueFormatter: (p) => (p.value ?? 0).toFixed(2) },
    {
      headerName: 'Sent to Payroll',
      field: 'sentToPayroll',
      width: 130,
      valueFormatter: (p) => (p.value ? 'Yes' : 'No'),
    },
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
    this.workloadService.estimatedHoursThisWeek().subscribe((w) => this.workload.set(w));
  }

  protected refresh(): void {
    this.timesheetEntries
      .list({ search: this.searchText() || undefined, onBehalfOfUserId: this.impersonation.actingAs()?.id })
      .subscribe((res) => {
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
    this.timesheetEntries.duplicate(entry.id, undefined, this.impersonation.actingAs()?.id).subscribe(() => this.refresh());
  }

  private delete(entry: TimesheetEntry): void {
    if (!confirm(`Delete this ${entry.workHours + entry.outOfHoursHours}h entry on ${entry.date}?`)) return;
    this.timesheetEntries.delete(entry.id, this.impersonation.actingAs()?.id).subscribe(() => this.refresh());
  }
}
