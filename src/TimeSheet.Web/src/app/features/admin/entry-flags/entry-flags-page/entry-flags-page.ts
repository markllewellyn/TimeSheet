import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { EntryFlagsService, EntryFlag } from '../../../../core/services/entry-flags.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-entry-flags-page',
  standalone: true,
  imports: [DatePipe, FormsModule, LoadingSpinner],
  templateUrl: './entry-flags-page.html',
})
export class EntryFlagsPage {
  private readonly entryFlagsService = inject(EntryFlagsService);
  private readonly route = inject(ActivatedRoute);
  protected readonly flags = signal<EntryFlag[]>([]);
  // Only ever set true->false, on the very first refresh() call - never reset for subsequent reloads after an
  // action (raise/clear), so those don't flash the spinner over an already-populated list.
  protected readonly loading = signal(true);

  // Set when arriving via a "View in Flags" click-through from the Log Time grid (see log-time-page.ts's
  // goToFlag) - highlights that one row and scrolls it into view, since this page otherwise has no per-flag
  // deep link of its own.
  protected readonly highlightedFlagId = signal<number | null>(null);

  protected readonly raiseEntryId = signal<number | null>(null);
  protected readonly raiseNotes = signal('');
  protected readonly raising = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly clearingFlagId = signal<number | null>(null);
  protected readonly clearNotes = signal('');
  protected readonly clearing = signal(false);

  constructor() {
    const flagIdParam = Number(this.route.snapshot.queryParamMap.get('flagId'));
    if (flagIdParam) this.highlightedFlagId.set(flagIdParam);
    this.refresh(flagIdParam > 0);
  }

  private refresh(scrollToHighlighted = false): void {
    this.entryFlagsService.listOpen().subscribe({
      next: (flags) => {
        this.flags.set(flags);
        this.loading.set(false);
        if (scrollToHighlighted) {
          const id = this.highlightedFlagId();
          setTimeout(() => document.getElementById(`flag-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.error ?? 'Could not load flags.');
      },
    });
  }

  protected raise(): void {
    const entryId = this.raiseEntryId();
    if (!entryId) return;
    this.raising.set(true);
    this.entryFlagsService.raiseManual(entryId, this.raiseNotes() || null).subscribe({
      next: () => {
        this.raising.set(false);
        this.raiseEntryId.set(null);
        this.raiseNotes.set('');
        this.error.set(null);
        this.refresh();
      },
      error: (err) => {
        this.raising.set(false);
        this.error.set(err?.error?.error ?? 'Could not raise the flag.');
      },
    });
  }

  protected startClear(f: EntryFlag): void {
    this.clearingFlagId.set(f.id);
    this.clearNotes.set('');
    this.error.set(null);
  }

  protected cancelClear(): void {
    this.clearingFlagId.set(null);
    this.clearNotes.set('');
  }

  protected confirmClear(f: EntryFlag): void {
    this.clearing.set(true);
    this.error.set(null);
    this.entryFlagsService.clear(f.id, this.clearNotes() || null).subscribe({
      next: () => {
        this.clearing.set(false);
        this.clearingFlagId.set(null);
        this.clearNotes.set('');
        this.refresh();
      },
      error: (err) => {
        this.clearing.set(false);
        this.error.set(err?.error?.error ?? 'Could not clear the flag.');
      },
    });
  }

  protected notifyStaff(f: EntryFlag): void {
    this.error.set(null);
    this.successMessage.set(null);
    this.entryFlagsService.notifyStaff(f.id).subscribe({
      next: () => this.successMessage.set(`Notified the staff member for entry #${f.timesheetEntryId}.`),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not notify the staff member.'),
    });
  }
}
