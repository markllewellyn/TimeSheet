import { KeyValuePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ClientsService } from '../../../../core/services/clients.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { ReportEnvelope, ReportRangePreset, ReportsService, ReportType } from '../../../../core/services/reports.service';
import { Client, Project } from '../../../../core/models/project.models';

const PROJECT_SCOPED: ReportType[] = ['time-on-project', 'cost-on-project', 'profit-on-project'];

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

  protected readonly breakdownColumns = computed(() => {
    const first = this.report()?.breakdown?.[0];
    return first ? Object.keys(first) : [];
  });

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
    this.reportsService
      .get(this.reportType(), scope, { preset: this.rangePreset(), startDate: this.startDate(), endDate: this.endDate() })
      .subscribe({
        next: (r) => this.report.set(r),
        error: (err) => this.error.set(err?.error?.error ?? 'Could not run the report.'),
      });
  }
}
