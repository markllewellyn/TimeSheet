import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MyOverviewService } from '../../../core/services/my-overview.service';
import { MyOverviewLine } from '../../../core/models/my-overview.models';

@Component({
  selector: 'app-my-overview-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './my-overview-page.html',
})
export class MyOverviewPage {
  private readonly myOverview = inject(MyOverviewService);

  protected readonly lines = signal<MyOverviewLine[]>([]);
  protected readonly loaded = signal(false);
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
    this.myOverview.get().subscribe((lines) => {
      this.lines.set(lines);
      this.loaded.set(true);
    });
  }
}
