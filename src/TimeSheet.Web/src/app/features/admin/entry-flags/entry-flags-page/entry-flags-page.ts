import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { EntryFlagsService, EntryFlag, EntryFlagSearchResult } from '../../../../core/services/entry-flags.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';
import { NotificationsService } from '../../../../core/services/notifications.service';

@Component({
  selector: 'app-entry-flags-page',
  standalone: true,
  imports: [DatePipe, FormsModule, LoadingSpinner],
  templateUrl: './entry-flags-page.html',
})
export class EntryFlagsPage {
  private readonly entryFlagsService = inject(EntryFlagsService);
  private readonly notificationsService = inject(NotificationsService);
  private readonly route = inject(ActivatedRoute);
  protected readonly flags = signal<EntryFlag[]>([]);
  // Only ever set true->false, on the very first refresh() call - never reset for subsequent reloads after an
  // action (raise/clear), so those don't flash the spinner over an already-populated list.
  protected readonly loading = signal(true);

  // Set when arriving via a "View in Flags" click-through from the Log Time grid (see log-time-page.ts's
  // goToFlag) - highlights that one row and scrolls it into view, since this page otherwise has no per-flag
  // deep link of its own.
  protected readonly highlightedFlagId = signal<number | null>(null);

  // Entry picker for "raise a flag" - a free-text search over staff/client/project/description or Entry Id,
  // live-as-you-type (same convention as Log Time's own search) rather than requiring the raw numeric
  // Timesheet Entry Id to be typed in by hand.
  protected readonly entrySearchQuery = signal('');
  protected readonly entrySearchResults = signal<EntryFlagSearchResult[]>([]);
  // True when the search matched more entries than the picker's 25-result cap shows - closes a real,
  // previously-unreproducible "my entry is missing from search" report by at least telling the user something
  // was cut, instead of silently truncating.
  protected readonly entrySearchHasMore = signal(false);
  protected readonly entrySearching = signal(false);
  protected readonly entrySearched = signal(false);
  protected readonly selectedEntry = signal<EntryFlagSearchResult | null>(null);

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

  protected onEntrySearchChange(value: string): void {
    this.entrySearchQuery.set(value);
    this.searchEntries();
  }

  private searchEntries(): void {
    const query = this.entrySearchQuery().trim();
    // Same minimum-length rule as the backend's own gate: a numeric query is an exact Entry Id lookup, so
    // even a single digit is meaningful; a text query needs 2+ characters to avoid a distractingly broad
    // match while the user is still mid-word. Below that, just show nothing rather than an error - this is
    // normal mid-typing state now that search runs live, not a mistake worth a banner for.
    const minLength = /^\d+$/.test(query) ? 1 : 2;
    if (query.length < minLength) {
      this.entrySearchResults.set([]);
      this.entrySearchHasMore.set(false);
      this.entrySearched.set(false);
      this.entrySearching.set(false);
      return;
    }

    this.entrySearching.set(true);
    this.entryFlagsService.searchEntries(query).subscribe({
      next: (response) => {
        if (this.entrySearchQuery().trim() !== query) return; // a newer keystroke's request already landed
        this.entrySearching.set(false);
        this.entrySearched.set(true);
        this.error.set(null);
        this.entrySearchResults.set(response.results);
        this.entrySearchHasMore.set(response.hasMore);
      },
      error: (err) => {
        if (this.entrySearchQuery().trim() !== query) return;
        this.entrySearching.set(false);
        this.entrySearched.set(true);
        this.entrySearchResults.set([]);
        this.entrySearchHasMore.set(false);
        this.error.set(err?.error?.error ?? 'Could not search entries.');
      },
    });
  }

  protected selectEntry(result: EntryFlagSearchResult): void {
    this.selectedEntry.set(result);
    this.entrySearchQuery.set('');
    this.entrySearchResults.set([]);
    this.entrySearchHasMore.set(false);
    this.entrySearched.set(false);
  }

  protected clearSelectedEntry(): void {
    this.selectedEntry.set(null);
  }

  protected raise(): void {
    const entry = this.selectedEntry();
    if (!entry) return;
    this.raising.set(true);
    this.entryFlagsService.raiseManual(entry.id, this.raiseNotes() || null).subscribe({
      next: () => {
        this.raising.set(false);
        this.selectedEntry.set(null);
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
      next: () => {
        this.successMessage.set(`Notified the staff member for entry #${f.timesheetEntryId}.`);
        // The recipient could be the current user (e.g. an Admin notifying themselves about their own
        // entry) - the bell otherwise wouldn't catch up until its next 90s poll or tab-focus refresh.
        this.notificationsService.refresh();
      },
      error: (err) => this.error.set(err?.error?.error ?? 'Could not notify the staff member.'),
    });
  }
}
