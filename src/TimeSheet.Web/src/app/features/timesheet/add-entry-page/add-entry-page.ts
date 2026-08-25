import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsService } from '../../../core/services/projects.service';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { Project } from '../../../core/models/project.models';

@Component({
  selector: 'app-add-entry-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './add-entry-page.html',
})
export class AddEntryPage {
  private readonly projectsService = inject(ProjectsService);
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;

  protected readonly projects = signal<Project[]>([]);
  protected readonly clients = computed(() => {
    const seen = new Map<number, string>();
    for (const p of this.projects()) seen.set(p.clientId, p.clientName);
    return Array.from(seen, ([id, name]) => ({ id, name }));
  });

  protected readonly selectedClientId = signal<number | null>(null);
  protected readonly projectsForClient = computed(() =>
    this.projects().filter((p) => p.clientId === this.selectedClientId()),
  );

  protected readonly selectedProjectId = signal<number | null>(null);
  protected readonly date = signal(new Date().toISOString().slice(0, 10));
  protected readonly workHours = signal(0);
  protected readonly outOfHoursHours = signal(0);
  protected readonly description = signal('');
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.projectsService.listAssignedToMe().subscribe((projects) => {
      this.projects.set(projects);
      if (this.editId) this.loadExisting(Number(this.editId));
    });
  }

  private loadExisting(id: number): void {
    this.timesheetEntries.getById(id).subscribe((entry) => {
      this.selectedClientId.set(entry.clientId);
      this.selectedProjectId.set(entry.projectId);
      this.date.set(entry.date);
      this.workHours.set(entry.workHours);
      this.outOfHoursHours.set(entry.outOfHoursHours);
      this.description.set(entry.description ?? '');
    });
  }

  protected onClientChange(clientId: number): void {
    this.selectedClientId.set(clientId);
    this.selectedProjectId.set(null);
  }

  protected save(): void {
    const projectId = this.selectedProjectId();
    if (!projectId) {
      this.error.set('Please select a customer and project.');
      return;
    }

    const request = {
      date: this.date(),
      workHours: this.workHours(),
      outOfHoursHours: this.outOfHoursHours(),
      description: this.description() || null,
    };

    const save$ = this.isEditMode
      ? this.timesheetEntries.update(Number(this.editId), request)
      : this.timesheetEntries.create({ projectId, ...request });

    save$.subscribe({
      next: () => this.router.navigate(['/timesheet']),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not save the entry.'),
    });
  }

  protected cancel(): void {
    this.router.navigate(['/timesheet']);
  }
}
