import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectsService } from '../../../core/services/projects.service';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { EntryTypesService, EntryType } from '../../../core/services/entry-types.service';
import { Project } from '../../../core/models/project.models';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { ImpersonationService } from '../../../core/services/impersonation.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-add-entry-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
  templateUrl: './add-entry-page.html',
})
export class AddEntryPage {
  private readonly projectsService = inject(ProjectsService);
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly entryTypesService = inject(EntryTypesService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly currentUser = inject(CurrentUserService);
  protected readonly impersonation = inject(ImpersonationService);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly projects = signal<Project[]>([]);
  protected readonly projectsLoaded = signal(false);
  // Only relevant/checked in edit mode (see `loading` below) - never set true when creating a brand-new entry,
  // since there's no existing record to wait for and isEditMode short-circuits before this value matters.
  protected readonly entryLoaded = signal(false);
  protected readonly loading = computed(() => !this.projectsLoaded() || (this.isEditMode && !this.entryLoaded()));
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

  // Only ever populated for projects with EntryTypes configured against them - most projects have none yet,
  // in which case the picker simply doesn't appear (EntryTypeId stays optional server-side too).
  protected readonly entryTypes = signal<EntryType[]>([]);
  protected readonly selectedEntryTypeId = signal<number | null>(null);

  // FDD: "the user can choose to add it to the current billing period or to the next billing period." Only
  // meaningful for a Time & Materials project - a Fixed Fee project's invoiced amount doesn't derive from
  // entries at all, so the picker is hidden rather than shown-but-inert there.
  protected readonly selectedProject = computed(() => this.projects().find((p) => p.id === this.selectedProjectId()));
  protected readonly showBillingPeriodChoice = computed(() => this.selectedProject()?.paymentModel === 'TimeAndMaterials');
  protected readonly billingPeriodChoice = signal<'Current' | 'Next'>('Current');

  constructor() {
    // Impersonation applies to both logging a brand-new entry and editing an existing one - an Admin can
    // impersonate a user and edit that user's timesheet entries while impersonating (FDD). Enforced
    // server-side regardless (TimesheetEntriesFunctions.CheckOwnership).
    const onBehalfOf = this.impersonation.actingAs();
    this.projectsService.listAssignedToMe(onBehalfOf?.id).subscribe({
      next: (projects) => {
        this.projects.set(projects);
        this.projectsLoaded.set(true);
        if (this.editId) this.loadExisting(Number(this.editId));
      },
      error: (err) => {
        this.projectsLoaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load your assigned projects.');
      },
    });

    effect(() => {
      const projectId = this.selectedProjectId();
      if (!projectId) {
        this.entryTypes.set([]);
        return;
      }
      this.entryTypesService.listByProject(projectId, false).subscribe((entryTypes) => this.entryTypes.set(entryTypes));
    });
  }

  private loadExisting(id: number): void {
    this.timesheetEntries.getById(id, this.impersonation.actingAs()?.id).subscribe({
      next: (entry) => {
        this.selectedClientId.set(entry.clientId);
        this.selectedProjectId.set(entry.projectId);
        this.date.set(entry.date);
        this.workHours.set(entry.workHours);
        this.outOfHoursHours.set(entry.outOfHoursHours);
        this.description.set(entry.description ?? '');
        this.selectedEntryTypeId.set(entry.entryTypeId);
        this.billingPeriodChoice.set(entry.billingPeriodChoice);
        this.entryLoaded.set(true);
      },
      error: (err) => {
        this.entryLoaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load this entry.');
      },
    });
  }

  protected onClientChange(clientId: number): void {
    this.selectedClientId.set(clientId);
    this.selectedProjectId.set(null);
    this.selectedEntryTypeId.set(null);
  }

  protected onProjectChange(projectId: number): void {
    this.selectedProjectId.set(projectId);
    this.selectedEntryTypeId.set(null);
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
      onBehalfOfUserId: this.impersonation.actingAs()?.id ?? null,
      entryTypeId: this.selectedEntryTypeId(),
      billingPeriodChoice: this.billingPeriodChoice(),
    };

    const save$ = this.isEditMode
      ? this.timesheetEntries.update(Number(this.editId), request)
      : this.timesheetEntries.create({
          projectId,
          ...request,
          adminSendToPayroll: this.adminSendToPayroll(),
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
