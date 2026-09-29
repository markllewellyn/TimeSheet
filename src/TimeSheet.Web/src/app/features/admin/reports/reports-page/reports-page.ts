import { KeyValuePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ClientsService } from '../../../../core/services/clients.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { ReportEnvelope, ReportRangePreset, ReportsService, ReportType } from '../../../../core/services/reports.service';
import { Client, Project } from '../../../../core/models/project.models';

const PROJECT_SCOPED: ReportType[] = [
  'time-on-project', 'cost-on-project', 'profit-on-project',
  'time-on-project-by-role', 'cost-on-project-by-role', 'profit-on-project-by-role',
  'time-on-project-by-team', 'cost-on-project-by-team', 'profit-on-project-by-team',
];

@Component({
  selector: 'app-reports-page',
  standalone: true,
  imports: [FormsModule, KeyValuePipe],
  templateUrl: './reports-page.html',
})
export class ReportsPage {
  private readonly clientsService = inject(ClientsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly reportsService = inject(ReportsService);

  protected readonly reportType = signal<ReportType>('time-on-client');
  protected readonly isProjectScoped = computed(() => PROJECT_SCOPED.includes(this.reportType()));

  protected readonly clients = signal<Client[]>([]);
  protected readonly projects = signal<Project[]>([]);
  protected readonly selectedClientId = signal<number | null>(null);
  protected readonly selectedProjectId = signal<number | null>(null);

  protected readonly rangePreset = signal<ReportRangePreset>('LastMonth');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly endDate = signal(new Date().toISOString().slice(0, 10));

  protected readonly report = signal<ReportEnvelope | null>(null);
  protected readonly error = signal<string | null>(null);

  // On a Fixed Fee project each breakdown row's Billed is its hours-share of the project's recognized revenue
  // (ReportingService), not hours x rate - shown as a one-line note. Captured when Run is clicked, so changing
  // the dropdowns afterwards can't relabel an already-displayed report.
  protected readonly fixedFeeSplitNote = signal(false);

  protected readonly breakdownColumns = computed(() => {
    const first = this.report()?.breakdown?.[0];
    return first ? Object.keys(first) : [];
  });

  /** camelCase API field name -> human label, e.g. "entryCount" -> "Entry Count". Used for both the summary
   * tiles and the breakdown table headers, since both render whatever fields the report happens to return. */
  protected columnLabel(key: string): string {
    return key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());
  }

  constructor() {
    this.clientsService.list().subscribe((clients) => this.clients.set(clients));
  }

  protected onClientChange(clientId: number): void {
    this.selectedClientId.set(clientId);
    this.selectedProjectId.set(null);
    this.projectsAdmin.listByClient(clientId, false).subscribe((projects) => this.projects.set(projects));
  }

  protected run(): void {
    const scope = this.isProjectScoped()
      ? { projectId: this.selectedProjectId() ?? undefined }
      : { clientId: this.selectedClientId() ?? undefined };

    if (this.isProjectScoped() && !scope.projectId) {
      this.error.set('Please select a client and project.');
      return;
    }
    if (!this.isProjectScoped() && !scope.clientId) {
      this.error.set('Please select a client.');
      return;
    }

    this.error.set(null);
    const project = this.projects().find((p) => p.id === this.selectedProjectId());
    const showSplitNote = this.reportType().startsWith('profit-on-project')
      && project?.paymentModel === 'FixedProjectCost'
      && project.canInvoice === true;
    this.reportsService
      .get(this.reportType(), scope, { preset: this.rangePreset(), startDate: this.startDate(), endDate: this.endDate() })
      .subscribe({
        next: (r) => {
          this.fixedFeeSplitNote.set(showSplitNote);
          this.report.set(r);
        },
        error: (err) => this.error.set(err?.error?.error ?? 'Could not run the report.'),
      });
  }
}
