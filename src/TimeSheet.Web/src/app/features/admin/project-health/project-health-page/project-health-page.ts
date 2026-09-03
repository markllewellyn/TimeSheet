import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ProjectHealthAssessment, ProjectHealthService } from '../../../../core/services/project-health.service';

@Component({
  selector: 'app-project-health-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './project-health-page.html',
})
export class ProjectHealthPage {
  private readonly projectHealthService = inject(ProjectHealthService);
  protected readonly assessments = signal<ProjectHealthAssessment[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly running = signal(false);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.projectHealthService.getDashboard().subscribe({
      next: (assessments) => this.assessments.set(assessments),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not load the project health dashboard.'),
    });
  }

  protected reassess(a: ProjectHealthAssessment): void {
    this.error.set(null);
    this.projectHealthService.reassess(a.projectId).subscribe({
      next: () => this.refresh(),
      error: (err) => this.error.set(err?.error?.error ?? `Could not reassess ${a.projectName}.`),
    });
  }

  protected runNow(): void {
    this.running.set(true);
    this.error.set(null);
    this.successMessage.set(null);
    this.projectHealthService.runNow().subscribe({
      next: (result) => {
        this.running.set(false);
        this.successMessage.set(
          result.failed > 0
            ? `Assessed ${result.assessed} project(s); ${result.failed} failed - check the API logs (likely a missing/invalid AI provider API key).`
            : `Assessed ${result.assessed} project(s).`,
        );
        this.refresh();
      },
      error: (err) => {
        this.running.set(false);
        this.error.set(err?.error?.error ?? 'Could not run the project health sweep.');
      },
    });
  }

  protected statusClass(status: string): string {
    switch (status) {
      case 'Behind':
        return 'badge-danger';
      case 'AtRisk':
        return 'badge-warning';
      default:
        return 'badge-success';
    }
  }
}
