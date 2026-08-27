import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EntryFlagsService, EntryFlag } from '../../../../core/services/entry-flags.service';

@Component({
  selector: 'app-entry-flags-page',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './entry-flags-page.html',
})
export class EntryFlagsPage {
  private readonly entryFlagsService = inject(EntryFlagsService);
  protected readonly flags = signal<EntryFlag[]>([]);

  protected readonly raiseEntryId = signal<number | null>(null);
  protected readonly raiseNotes = signal('');
  protected readonly raising = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.entryFlagsService.listOpen().subscribe((flags) => this.flags.set(flags));
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

  protected clear(f: EntryFlag): void {
    const notes = prompt('Clearing notes (optional):');
    this.entryFlagsService.clear(f.id, notes || null).subscribe(() => this.refresh());
  }

  protected notifyStaff(f: EntryFlag): void {
    this.entryFlagsService.notifyStaff(f.id).subscribe();
  }
}
