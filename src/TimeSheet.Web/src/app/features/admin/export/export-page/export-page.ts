import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminExportService } from '../../../../core/services/admin-export.service';

@Component({
  selector: 'app-export-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './export-page.html',
})
export class ExportPage {
  private readonly adminExport = inject(AdminExportService);

  protected readonly startDate = signal('');
  protected readonly endDate = signal('');
  protected readonly downloading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected download(): void {
    this.downloading.set(true);
    this.error.set(null);
    this.adminExport.downloadTimesheetEntriesCsv(this.startDate() || undefined, this.endDate() || undefined).subscribe({
      next: (blob) => {
        this.downloading.set(false);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `timesheet-export-${new Date().toISOString().slice(0, 10)}.csv`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.downloading.set(false);
        this.error.set(err?.error?.error ?? 'Could not generate the export.');
      },
    });
  }
}
