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

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.projectHealthService.getDashboard().subscribe((assessments) => this.assessments.set(assessments));
  }

  protected reassess(a: ProjectHealthAssessment): void {
    this.projectHealthService.reassess(a.projectId).subscribe(() => this.refresh());
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
