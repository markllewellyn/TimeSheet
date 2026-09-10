import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { ProjectsAdminService } from '../../../core/services/projects-admin.service';
import { Project, ProjectBreakdown } from '../../../core/models/project.models';
import { chartColor } from '../../../core/utils/chart-palette';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';
import { ChartCanvas } from '../../../core/components/chart-canvas/chart-canvas';

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/**
 * User-requested drill-down from the Budget & Cost Status panel/badges: "who has done what, and on what" for
 * a project's actual logged time to date. Reachable by Admin or the project's own nominated PM (same gate as
 * Projects_Breakdown) - no adminGuard on this route, so a PM linked in from My Projects can actually reach it.
 */
@Component({
  selector: 'app-project-detail-page',
  standalone: true,
  imports: [DecimalPipe, LoadingSpinner, ChartCanvas],
  templateUrl: './project-detail-page.html',
})
export class ProjectDetailPage {
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly route = inject(ActivatedRoute);

  private readonly id = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly project = signal<Project | null>(null);
  protected readonly breakdown = signal<ProjectBreakdown | null>(null);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly byStaffChart = computed<ChartConfiguration['data']>(() => {
    const lines = this.breakdown()?.byStaff ?? [];
    return {
      labels: lines.map((l) => l.userName),
      datasets: [{ data: lines.map((l) => l.hours), backgroundColor: lines.map((_, i) => chartColor(i)) }],
    };
  });

  protected readonly byEntryTypeChart = computed<ChartConfiguration['data']>(() => {
    const lines = this.breakdown()?.byEntryType ?? [];
    return {
      labels: lines.map((l) => l.entryTypeName),
      datasets: [{ data: lines.map((l) => l.hours), backgroundColor: lines.map((_, i) => chartColor(i)) }],
    };
  });

  protected readonly byMonthChart = computed<ChartConfiguration['data']>(() => {
    const lines = this.breakdown()?.byMonth ?? [];
    return {
      labels: lines.map((l) => `${MONTH_NAMES[l.month - 1]} ${l.year}`),
      datasets: [{ label: 'Hours', data: lines.map((l) => l.hours), backgroundColor: chartColor(0) }],
    };
  });

  constructor() {
    this.projectsAdmin.getById(this.id).subscribe((p) => this.project.set(p));
    this.projectsAdmin.getBreakdown(this.id).subscribe({
      next: (b) => {
        this.breakdown.set(b);
        this.loaded.set(true);
      },
      error: (err) => {
        this.loaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load this project breakdown.');
      },
    });
  }
}
