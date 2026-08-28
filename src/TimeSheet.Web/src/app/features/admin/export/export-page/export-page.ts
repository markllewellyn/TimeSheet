import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminExportService } from '../../../../core/services/admin-export.service';
import { ClientsService } from '../../../../core/services/clients.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { ProjectsService } from '../../../../core/services/projects.service';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { CurrentUserService } from '../../../../core/auth/current-user.service';
import { Client, Project } from '../../../../core/models/project.models';
import { AppUser } from '../../../../core/models/user.models';

/**
 * FDD: "Entries can be exported to CSV... available... for both users and administrators. Data can
 * be exported on a client, project or project manager basis, for all users or for specific users.
 * Every data extract offers the last 7 days, the last calendar month, or a custom date range."
 * Client/Project-manager/specific-user filters are admin-only (cross-user data) - a non-admin only
 * ever gets their own entries, optionally narrowed to one of their own assigned projects; enforced
 * server-side regardless of what this page sends.
 */
@Component({
  selector: 'app-export-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './export-page.html',
})
export class ExportPage {
  private readonly adminExport = inject(AdminExportService);
  private readonly clientsService = inject(ClientsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly projectsService = inject(ProjectsService);
  private readonly usersAdmin = inject(UsersAdminService);
  protected readonly currentUser = inject(CurrentUserService);

  protected readonly clients = signal<Client[]>([]);
  protected readonly projects = signal<Project[]>([]);
  protected readonly users = signal<AppUser[]>([]);

  protected readonly startDate = signal('');
  protected readonly endDate = signal('');
  protected readonly clientId = signal<number | null>(null);
  protected readonly projectId = signal<number | null>(null);
  protected readonly projectManagerUserId = signal<number | null>(null);
  protected readonly userId = signal<number | null>(null);
  protected readonly downloading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.currentUser.isAdmin()) {
      this.clientsService.list().subscribe((clients) => this.clients.set(clients));
      this.projectsAdmin.listAll().subscribe((projects) => this.projects.set(projects));
      this.usersAdmin.list(false).subscribe((users) => this.users.set(users));
    } else {
      this.projectsService.listAssignedToMe().subscribe((projects) => this.projects.set(projects));
    }
  }

  protected applyPreset(preset: 'last7Days' | 'lastCalendarMonth'): void {
    const today = new Date();
    if (preset === 'last7Days') {
      const start = new Date(today);
      start.setDate(start.getDate() - 6);
      this.startDate.set(start.toISOString().slice(0, 10));
      this.endDate.set(today.toISOString().slice(0, 10));
      return;
    }

    // The most recently completed calendar month, not the current (in-progress) one.
    const firstOfThisMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    const lastOfPreviousMonth = new Date(firstOfThisMonth.getTime() - 1);
    const firstOfPreviousMonth = new Date(lastOfPreviousMonth.getFullYear(), lastOfPreviousMonth.getMonth(), 1);
    this.startDate.set(firstOfPreviousMonth.toISOString().slice(0, 10));
    this.endDate.set(lastOfPreviousMonth.toISOString().slice(0, 10));
  }

  protected download(): void {
    this.downloading.set(true);
    this.error.set(null);
    this.adminExport
      .downloadTimesheetEntriesCsv({
        from: this.startDate() || undefined,
        to: this.endDate() || undefined,
        projectId: this.projectId(),
        clientId: this.clientId(),
        userId: this.userId(),
        projectManagerUserId: this.projectManagerUserId(),
      })
      .subscribe({
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
