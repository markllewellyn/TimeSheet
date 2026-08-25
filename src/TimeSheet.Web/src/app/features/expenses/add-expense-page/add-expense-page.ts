import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ProjectsService } from '../../../core/services/projects.service';
import { ExpenseEntriesService } from '../../../core/services/expense-entries.service';
import { Project } from '../../../core/models/project.models';

@Component({
  selector: 'app-add-expense-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './add-expense-page.html',
})
export class AddExpensePage {
  private readonly projectsService = inject(ProjectsService);
  private readonly expenseEntries = inject(ExpenseEntriesService);
  private readonly router = inject(Router);

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
  protected readonly amount = signal(0);
  protected readonly currency = signal('GBP');
  protected readonly description = signal('');
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.projectsService.listAssignedToMe().subscribe((projects) => this.projects.set(projects));
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

    this.expenseEntries
      .create({
        projectId,
        date: this.date(),
        amount: this.amount(),
        currency: this.currency(),
        description: this.description() || null,
        isBillable: true,
      })
      .subscribe({
        next: () => this.router.navigate(['/timesheet']),
        error: (err) => this.error.set(err?.error?.error ?? 'Could not save the expense.'),
      });
  }

  protected cancel(): void {
    this.router.navigate(['/timesheet']);
  }
}
