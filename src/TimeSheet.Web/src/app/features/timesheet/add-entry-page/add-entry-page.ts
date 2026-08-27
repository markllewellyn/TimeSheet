import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsService } from '../../../core/services/projects.service';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { Project } from '../../../core/models/project.models';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { ImpersonationService } from '../../../core/services/impersonation.service';

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
  protected readonly currentUser = inject(CurrentUserService);
  protected readonly impersonation = inject(ImpersonationService);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly projects = signal<Project[]>([]);
  protected readonly projectsLoaded = signal(false);
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
  protected readonly adminSendToPayroll = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    // Impersonation only applies to logging a brand-new entry - editing an existing one always acts as that
    // entry's actual owner (enforced server-side by TimesheetEntries_Update's ownership check regardless).
    const onBehalfOf = this.isEditMode ? null : this.impersonation.actingAs();
    this.projectsService.listAssignedToMe(onBehalfOf?.id).subscribe((projects) => {
      this.projects.set(projects);
      this.projectsLoaded.set(true);
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
      this.error.set('Please select a client and project.');
      return;
    }

    // Mirrors the server-side rules in TimesheetEntriesFunctions.ValidateEntryValuesAsync for immediate
    // feedback - the "not before the client's start date" rule is server-only since the client list here
    // doesn't carry Client.StartDate; the server's error message still surfaces via the catch below.
    if (this.date() > this.today) {
      this.error.set('The date cannot be in the future.');
      return;
    }
    if (this.workHours() < 0 || this.outOfHoursHours() < 0) {
      this.error.set('Hours cannot be negative.');
      return;
    }
    if (this.workHours() + this.outOfHoursHours() <= 0) {
      this.error.set('At least one of Work Hours or Out of Hours must be greater than zero.');
      return;
    }
    if (this.workHours() + this.outOfHoursHours() > 24) {
      this.error.set('Total hours for a single entry cannot exceed 24.');
      return;
    }
    if (this.workHours() % 0.5 !== 0 || this.outOfHoursHours() % 0.5 !== 0) {
      this.error.set('Hours must be entered in half-hour increments.');
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
      : this.timesheetEntries.create({
          projectId,
          ...request,
          adminSendToPayroll: this.adminSendToPayroll(),
          onBehalfOfUserId: this.impersonation.actingAs()?.id ?? null,
        });

    this.saving.set(true);
    save$.subscribe({
      next: () => this.router.navigate(['/timesheet']),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not save the entry.');
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/timesheet']);
  }
}
