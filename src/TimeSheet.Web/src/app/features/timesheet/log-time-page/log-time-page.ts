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
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { ConfirmService } from '../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';

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
  imports: [AgGridAngular, LoadingSpinner],
  templateUrl: './log-time-page.html',
})
export class LogTimePage {
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly workloadService = inject(WorkloadService);
  private readonly router = inject(Router);
  protected readonly themeService = inject(ThemeService);
  protected readonly impersonation = inject(ImpersonationService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly confirmService = inject(ConfirmService);

  // Mirrors the :root.dark tokens in styles.scss so the grid matches the rest of the app.
  protected readonly theme = computed(() => (this.themeService.mode() === 'dark' ? DARK_GRID_THEME : LIGHT_GRID_THEME));
  protected readonly searchText = signal('');
  protected readonly summary = signal<TimesheetEntrySummary | null>(null);
  protected readonly rowData = signal<TimesheetEntry[]>([]);
  // Only ever true->false, on the first refresh() - subsequent refreshes (search, post-action reload) don't
  // reset it, so the grid doesn't get torn down and remounted on every keystroke.
  protected readonly loading = signal(true);
  protected readonly workload = signal<EstimatedWeeklyWorkload | null>(null);

  private readonly actionsContext: EntryActionsContext = {
    onEdit: (entry) => this.router.navigate(['/timesheet', entry.id, 'edit']),
    onDuplicate: (entry) => this.duplicate(entry),
    onDelete: (entry) => this.delete(entry),
  };

  protected readonly columnDefs: ColDef<TimesheetEntry>[] = [
    // Surfaced so a user can actually read off the number the Entry Flags "raise a flag" form asks for -
    // otherwise nowhere in the UI ever showed an entry's id at all.
    { headerName: 'Entry ID', field: 'id', width: 90, type: 'numericColumn' },
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
    {
      // A flag is never a gate (FDD) - purely informational, doesn't affect editability - so this is a plain
      // indicator, not a blocked/locked state like the entry-actions column's "Locked" text. Hover shows why
      // it's flagged (plain-text tooltip - reliable across browsers, unlike an interactive ag-grid tooltip);
      // clicking jumps to the Entry Flags admin page, but only for Admins - that page is Admin-only today, so
      // offering the click-through to anyone else would just lead them to a screen they can't use.
      headerName: 'Flagged',
      field: 'openFlags',
      width: 100,
      valueFormatter: (p) => (p.value?.length > 0 ? '⚑ Flagged' : ''),
      tooltipValueGetter: (p) => this.flagTooltipText(p.data),
      cellClass: (p) => (p.value?.length > 0 ? 'text-[var(--destructive)] font-medium' + (this.currentUser.isAdmin() ? ' cursor-pointer underline' : '') : ''),
      onCellClicked: (p) => this.goToFlag(p.data),
    },
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
      .subscribe({
        next: (res) => {
          this.rowData.set(res.entries);
          this.summary.set(res.summary);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected onSearchChange(value: string): void {
    this.searchText.set(value);
    this.refresh();
  }

  private flagTooltipText(entry: TimesheetEntry | undefined): string {
    if (!entry || entry.openFlags.length === 0) return '';
    return entry.openFlags
      .map((f) => {
        const when = new Date(f.raisedAtUtc).toLocaleDateString();
        const reason = f.reason === 'Manual' ? 'Flagged for query' : `Budget/allocation exceeded (${f.reason})`;
        return f.raisedNotes ? `${reason} (${when}): ${f.raisedNotes}` : `${reason} (${when})`;
      })
      .join('\n');
  }

  private goToFlag(entry: TimesheetEntry | undefined): void {
    if (!entry || entry.openFlags.length === 0 || !this.currentUser.isAdmin()) return;
    this.router.navigate(['/admin/entry-flags'], { queryParams: { flagId: entry.openFlags[0].id } });
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

  private async delete(entry: TimesheetEntry): Promise<void> {
    const confirmed = await this.confirmService.confirm(
      `Delete this ${entry.workHours + entry.outOfHoursHours}h entry on ${entry.date}?`,
      { confirmLabel: 'Delete', destructive: true },
    );
    if (!confirmed) return;
    this.timesheetEntries.delete(entry.id, this.impersonation.actingAs()?.id).subscribe(() => this.refresh());
  }
}
