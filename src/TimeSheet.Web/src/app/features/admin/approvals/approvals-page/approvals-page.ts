import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApprovalsService } from '../../../../core/services/approvals.service';
import { ApprovalEntry } from '../../../../core/models/approval.models';

@Component({
  selector: 'app-approvals-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './approvals-page.html',
})
export class ApprovalsPage {
  private readonly approvalsService = inject(ApprovalsService);

  protected readonly activeTab = signal<'pending' | 'ready'>('pending');
  protected readonly error = signal<string | null>(null);

  // --- Approval queue tab ---
  protected readonly searchText = signal('');
  protected readonly pending = signal<ApprovalEntry[]>([]);
  protected readonly postingBatches = signal<string[]>([]);
  protected readonly selectedPendingIds = signal<Set<number>>(new Set());
  protected readonly postingBatchChoice = signal('');
  protected readonly newPostingBatchName = signal('');
  protected readonly approving = signal(false);

  protected readonly pendingTotals = computed(() => {
    const rows = this.pending();
    return {
      workHours: rows.reduce((s, r) => s + r.workHours, 0),
      outOfHoursHours: rows.reduce((s, r) => s + r.outOfHoursHours, 0),
      toPayroll: rows.reduce((s, r) => s + r.toPayroll, 0),
    };
  });

  // --- Ready for payroll tab ---
  protected readonly ready = signal<ApprovalEntry[]>([]);
  protected readonly readyLoaded = signal(false);
  protected readonly selectedReadyIds = signal<Set<number>>(new Set());
  protected readonly sending = signal(false);

  protected readonly readyTotals = computed(() => {
    const rows = this.ready();
    return { toPayroll: rows.reduce((s, r) => s + r.toPayroll, 0) };
  });

  constructor() {
    this.refreshPending();
  }

  protected switchTab(tab: 'pending' | 'ready'): void {
    this.activeTab.set(tab);
    if (tab === 'ready' && !this.readyLoaded()) this.refreshReady();
  }

  protected refreshPending(): void {
    this.approvalsService.pending(this.searchText() || undefined).subscribe((res) => {
      this.pending.set(res.entries);
      this.postingBatches.set(res.postingBatches);
      this.selectedPendingIds.set(new Set());
    });
  }

  protected refreshReady(): void {
    this.approvalsService.readyForPayroll().subscribe((rows) => {
      this.ready.set(rows);
      this.readyLoaded.set(true);
      this.selectedReadyIds.set(new Set());
    });
  }

  protected togglePendingSelected(id: number): void {
    const next = new Set(this.selectedPendingIds());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selectedPendingIds.set(next);
  }

  protected toggleSelectAllPending(): void {
    this.selectedPendingIds.set(
      this.selectedPendingIds().size === this.pending().length ? new Set() : new Set(this.pending().map((e) => e.id)),
    );
  }

  protected approveSelected(): void {
    const ids = Array.from(this.selectedPendingIds());
    const batch = this.postingBatchChoice() || this.newPostingBatchName();
    if (ids.length === 0) {
      this.error.set('Select at least one entry to approve.');
      return;
    }
    if (!batch) {
      this.error.set('Choose an existing posting batch or enter a new one.');
      return;
    }

    this.approving.set(true);
    this.approvalsService.approve(ids, batch).subscribe({
      next: () => {
        this.approving.set(false);
        this.error.set(null);
        this.newPostingBatchName.set('');
        this.postingBatchChoice.set('');
        this.refreshPending();
        // Approving moves entries into the "ready" queue - keep its tab badge/list in sync
        // immediately rather than leaving it stale until the user switches tabs or reloads.
        if (this.readyLoaded()) this.refreshReady();
      },
      error: (err) => {
        this.approving.set(false);
        this.error.set(err?.error?.error ?? 'Could not approve the selected entries.');
      },
    });
  }

  protected toggleReadySelected(id: number): void {
    const next = new Set(this.selectedReadyIds());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selectedReadyIds.set(next);
  }

  protected toggleSelectAllReady(): void {
    this.selectedReadyIds.set(
      this.selectedReadyIds().size === this.ready().length ? new Set() : new Set(this.ready().map((e) => e.id)),
    );
  }

  protected sendSelected(): void {
    const ids = Array.from(this.selectedReadyIds());
    if (ids.length === 0) {
      this.error.set('Select at least one entry to send.');
      return;
    }

    this.sending.set(true);
    this.approvalsService.sendToPayroll(ids).subscribe({
      next: () => {
        this.sending.set(false);
        this.error.set(null);
        this.refreshReady();
      },
      error: (err) => {
        this.sending.set(false);
        this.error.set(err?.error?.error ?? 'Could not send the selected entries to payroll.');
      },
    });
  }
}
