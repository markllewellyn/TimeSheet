import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MyOverviewService } from '../../../core/services/my-overview.service';
import { MyOverviewLine } from '../../../core/models/my-overview.models';
import { ImpersonationService } from '../../../core/services/impersonation.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-my-overview-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe, LoadingSpinner],
  templateUrl: './my-overview-page.html',
})
export class MyOverviewPage {
  private readonly myOverview = inject(MyOverviewService);
  private readonly impersonation = inject(ImpersonationService);

  protected readonly lines = signal<MyOverviewLine[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly searchText = signal('');
  protected readonly sentToPayrollOnly = signal(false);

  protected readonly visibleLines = computed(() => {
    const term = this.searchText().trim().toLowerCase();
    return this.lines().filter((l) => {
      if (this.sentToPayrollOnly() && !l.sentToPayroll) return false;
      if (!term) return true;
      return l.clientName.toLowerCase().includes(term) || l.projectName.toLowerCase().includes(term);
    });
  });

  protected readonly totals = computed(() => {
    const rows = this.visibleLines();
    return {
      workHours: rows.reduce((s, r) => s + r.workHours, 0),
      outOfHoursHours: rows.reduce((s, r) => s + r.outOfHoursHours, 0),
      toPayroll: rows.reduce((s, r) => s + r.toPayroll, 0),
    };
  });

  constructor() {
    // See the identical comment in log-time-page.ts - the header's impersonation picker doesn't navigate
    // away/back, so without this the overview kept showing your own hours after switching who you're acting as.
    effect(() => {
      this.impersonation.actingAs();
      untracked(() => this.refresh());
    });
  }

  private refresh(): void {
    this.myOverview.get(this.impersonation.actingAs()?.id).subscribe({
      next: (lines) => {
        this.lines.set(lines);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load your overview.');
      },
    });
  }
}
