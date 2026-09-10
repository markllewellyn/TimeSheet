import { Component, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProjectsAdminService } from '../../../core/services/projects-admin.service';
import { ImpersonationService } from '../../../core/services/impersonation.service';
import { Project } from '../../../core/models/project.models';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';
import { ProjectStatusBadges } from '../../../core/components/project-status-badges/project-status-badges';

/**
 * FDD: "a project manager is nominated against each project" who can "view/query" it. Read-only, scoped
 * server-side to the caller's own managed project(s) (Projects_ListManagedByMe) - mirrors My Invoices'
 * "route stays open to any signed-in user, backend does the scoping" pattern exactly. Flat list only, no
 * separate detail page - the badges already show everything a PM needs at a glance, same as My Invoices never
 * drills into a per-invoice detail view either. Also honours impersonation (same effect()-on-actingAs pattern
 * as expenses-list-page/my-overview-page) so an Admin impersonating a PM sees that PM's own project(s).
 */
@Component({
  selector: 'app-my-projects-page',
  standalone: true,
  imports: [RouterLink, LoadingSpinner, ProjectStatusBadges],
  templateUrl: './my-projects-page.html',
})
export class MyProjectsPage {
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly impersonation = inject(ImpersonationService);

  protected readonly projects = signal<Project[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.impersonation.actingAs();
      untracked(() => this.refresh());
    });
  }

  private refresh(): void {
    this.projectsAdmin.listManagedByMe(this.impersonation.actingAs()?.id).subscribe({
      next: (projects) => {
        this.projects.set(projects);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load your projects.');
      },
    });
  }
}
