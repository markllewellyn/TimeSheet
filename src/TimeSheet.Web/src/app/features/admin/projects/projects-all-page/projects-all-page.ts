import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ClientsService } from '../../../../core/services/clients.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { Project } from '../../../../core/models/project.models';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

/**
 * A flat, all-clients view of Projects - the per-client project list (admin/clients/:clientId/projects) is
 * still where a Project actually gets created, since a Project always belongs to exactly one Client, but this
 * gives Admins a single "Projects" entry point in the top nav instead of having to go via Clients first.
 */
@Component({
  selector: 'app-projects-all-page',
  standalone: true,
  imports: [RouterLink, LoadingSpinner],
  templateUrl: './projects-all-page.html',
})
export class ProjectsAllPage {
  private readonly clientsService = inject(ClientsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);

  protected readonly projects = signal<Project[]>([]);
  protected readonly loaded = signal(false);

  constructor() {
    this.clientsService.list().subscribe((clients) => {
      if (clients.length === 0) {
        this.loaded.set(true);
        return;
      }

      forkJoin(clients.map((c) => this.projectsAdmin.listByClient(c.id))).subscribe({
        next: (results) => {
          this.projects.set(results.flat().sort((a, b) => a.clientName.localeCompare(b.clientName) || a.name.localeCompare(b.name)));
          this.loaded.set(true);
        },
        error: () => {
          this.projects.set([]);
          this.loaded.set(true);
        },
      });
    });
  }
}
