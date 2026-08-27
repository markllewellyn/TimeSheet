import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../../core/services/projects.service';
import { ProjectsAdminService } from '../../../core/services/projects-admin.service';
import { ClientsService } from '../../../core/services/clients.service';
import { ExpenseEntriesService } from '../../../core/services/expense-entries.service';
import { Project } from '../../../core/models/project.models';
import { CurrentUserService } from '../../../core/auth/current-user.service';

type EntryKind = 'Expense' | 'Contract';

@Component({
  selector: 'app-add-expense-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './add-expense-page.html',
})
export class AddExpensePage {
  private readonly projectsService = inject(ProjectsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly clientsService = inject(ClientsService);
  private readonly expenseEntries = inject(ExpenseEntriesService);
  private readonly router = inject(Router);
  protected readonly currentUser = inject(CurrentUserService);

  protected readonly kind = signal<EntryKind>('Expense');
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
  protected readonly amount = signal(0);
  protected readonly currency = signal('GBP');
  protected readonly description = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.loadProjectsFor('Expense');
  }

  protected onKindChange(kind: EntryKind): void {
    this.kind.set(kind);
    this.selectedClientId.set(null);
    this.selectedProjectId.set(null);
    this.loadProjectsFor(kind);
  }

  private loadProjectsFor(kind: EntryKind): void {
    this.projectsLoaded.set(false);

    if (kind === 'Expense') {
      this.projectsService.listAssignedToMe().subscribe((projects) => {
        this.projects.set(projects);
        this.projectsLoaded.set(true);
      });
      return;
    }

    // Contract entries aren't tied to the admin's own assignments - they're logged against any
    // non-invoiceable project across all clients, same "all clients" fetch pattern as projects-all-page.
    this.clientsService.list().subscribe((clientsList) => {
      if (clientsList.length === 0) {
        this.projects.set([]);
        this.projectsLoaded.set(true);
        return;
      }
      forkJoin(clientsList.map((c) => this.projectsAdmin.listByClient(c.id))).subscribe({
        next: (results) => {
          this.projects.set(results.flat().filter((p) => p.canInvoice !== true));
          this.projectsLoaded.set(true);
        },
        error: () => {
          this.projects.set([]);
          this.projectsLoaded.set(true);
        },
      });
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

    this.saving.set(true);
    this.expenseEntries
      .create({
        projectId,
        date: this.date(),
        amount: this.amount(),
        currency: this.currency(),
        description: this.description() || null,
        isBillable: true,
        kind: this.kind(),
      })
      .subscribe({
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
