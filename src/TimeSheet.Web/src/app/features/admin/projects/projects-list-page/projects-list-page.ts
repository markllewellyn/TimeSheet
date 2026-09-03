import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { ClientsService } from '../../../../core/services/clients.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';
import { Project } from '../../../../core/models/project.models';

@Component({
  selector: 'app-projects-list-page',
  standalone: true,
  imports: [RouterLink, FormsModule, LoadingSpinner],
  templateUrl: './projects-list-page.html',
})
export class ProjectsListPage {
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly clientsService = inject(ClientsService);
  private readonly route = inject(ActivatedRoute);

  protected readonly clientId = Number(this.route.snapshot.paramMap.get('clientId'));
  protected readonly clientName = signal('');
  protected readonly projects = signal<Project[]>([]);
  protected readonly activeOnly = signal(true);
  protected readonly loading = signal(true);
  protected readonly visibleProjects = computed(() =>
    this.activeOnly() ? this.projects().filter((p) => p.isActive) : this.projects(),
  );

  constructor() {
    this.clientsService.getById(this.clientId).subscribe((c) => this.clientName.set(c.name));
    this.projectsAdmin.listByClient(this.clientId).subscribe({
      next: (projects) => {
        this.projects.set(projects);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
