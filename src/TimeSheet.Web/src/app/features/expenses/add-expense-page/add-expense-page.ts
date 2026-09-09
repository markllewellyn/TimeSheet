import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../../core/services/projects.service';
import { ProjectsAdminService } from '../../../core/services/projects-admin.service';
import { ClientsService } from '../../../core/services/clients.service';
import { ExpenseEntriesService } from '../../../core/services/expense-entries.service';
import { Project } from '../../../core/models/project.models';
import { Attachment } from '../../../core/models/timesheet-entry.models';
import { ExpenseEntry } from '../../../core/models/expense-entry.models';
import { CurrentUserService } from '../../../core/auth/current-user.service';
import { ConfirmService } from '../../../core/services/confirm.service';
import { ImpersonationService } from '../../../core/services/impersonation.service';
import { LoadingSpinner } from '../../../core/components/loading-spinner/loading-spinner';

type EntryKind = 'Expense' | 'Contract';

@Component({
  selector: 'app-add-expense-page',
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe, LoadingSpinner],
  templateUrl: './add-expense-page.html',
})
export class AddExpensePage {
  private readonly projectsService = inject(ProjectsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly clientsService = inject(ClientsService);
  private readonly expenseEntries = inject(ExpenseEntriesService);
  private readonly confirmService = inject(ConfirmService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly currentUser = inject(CurrentUserService);

  private readonly editId = this.route.snapshot.paramMap.get('id');
  protected readonly isEditMode = this.editId !== null;

  protected readonly kind = signal<EntryKind>('Expense');
  protected readonly projects = signal<Project[]>([]);
  protected readonly projectsLoaded = signal(false);
  // Only relevant/checked in edit mode - never set true when creating a brand-new entry, since there's no
  // existing record to wait for and isEditMode short-circuits before this value matters.
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
  protected readonly amount = signal(0);
  protected readonly currency = signal('GBP');
  protected readonly description = signal('');
  protected readonly isBillable = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly attachments = signal<Attachment[]>([]);
  protected readonly uploadingAttachment = signal(false);
  protected readonly attachmentsError = signal<string | null>(null);

  constructor() {
    if (this.isEditMode) {
      this.loadExisting(Number(this.editId));
    } else {
      this.loadProjectsFor('Expense');
    }
  }

  private loadExisting(id: number): void {
    this.expenseEntries.getById(id, this.impersonation.actingAs()?.id).subscribe({
      next: (entry) => {
        this.kind.set(entry.kind);
        this.loadProjectsFor(entry.kind, () => {
          this.selectedClientId.set(entry.clientId);
          this.selectedProjectId.set(entry.projectId);
          this.date.set(entry.date);
          this.amount.set(entry.amount);
          this.currency.set(entry.currency);
          this.description.set(entry.description ?? '');
          this.isBillable.set(entry.isBillable);
          this.attachments.set(entry.attachments);
          this.entryLoaded.set(true);
        });
      },
      error: (err) => {
        this.projectsLoaded.set(true);
        this.entryLoaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load this entry.');
      },
    });
  }

  protected onKindChange(kind: EntryKind): void {
    this.kind.set(kind);
    this.selectedClientId.set(null);
    this.selectedProjectId.set(null);
    this.loadProjectsFor(kind);
  }

  private loadProjectsFor(kind: EntryKind, onLoaded?: () => void): void {
    this.projectsLoaded.set(false);

    if (kind === 'Expense') {
      this.projectsService.listAssignedToMe(this.impersonation.actingAs()?.id).subscribe({
        next: (projects) => {
          this.projects.set(projects);
          this.projectsLoaded.set(true);
          onLoaded?.();
        },
        error: (err) => {
          this.projectsLoaded.set(true);
          this.error.set(err?.error?.error ?? 'Could not load your assigned projects.');
        },
      });
      return;
    }

    // Contract entries aren't tied to the admin's own assignments - they're logged against any
    // non-invoiceable project across all clients, same "all clients" fetch pattern as projects-all-page.
    this.clientsService.list().subscribe({
      next: (clientsList) => {
        if (clientsList.length === 0) {
          this.projects.set([]);
          this.projectsLoaded.set(true);
          onLoaded?.();
          return;
        }
        forkJoin(clientsList.map((c) => this.projectsAdmin.listByClient(c.id))).subscribe({
          next: (results) => {
            this.projects.set(results.flat().filter((p) => p.canInvoice !== true));
            this.projectsLoaded.set(true);
            onLoaded?.();
          },
          error: () => {
            this.projects.set([]);
            this.projectsLoaded.set(true);
            onLoaded?.();
          },
        });
      },
      error: (err) => {
        this.projectsLoaded.set(true);
        this.error.set(err?.error?.error ?? 'Could not load clients.');
      },
    });
  }

  protected onClientChange(clientId: number): void {
    this.selectedClientId.set(clientId);
    this.selectedProjectId.set(null);
  }

  protected save(): void {
    this.performSave(() => this.router.navigate(['/expenses']));
  }

  // Create-mode only (see the template's [!isEditMode] gate) - lets a brand-new entry go straight to its own
  // Edit page instead of back to the list, since that's the only place the Attachments card lives (it needs a
  // real entry Id, which doesn't exist until this save completes).
  protected saveAndAttach(): void {
    this.performSave((entry) => this.router.navigate(['/expenses', entry.id, 'edit']));
  }

  private performSave(onSuccess: (entry: ExpenseEntry) => void): void {
    const projectId = this.selectedProjectId();
    if (!projectId) {
      this.error.set('Please select a client and project.');
      return;
    }

    this.saving.set(true);

    const onBehalfOfUserId = this.impersonation.actingAs()?.id ?? null;

    const save$ = this.isEditMode
      ? this.expenseEntries.update(Number(this.editId), {
          date: this.date(),
          amount: this.amount(),
          currency: this.currency(),
          description: this.description() || null,
          isBillable: this.isBillable(),
          onBehalfOfUserId,
        })
      : this.expenseEntries.create({
          projectId,
          date: this.date(),
          amount: this.amount(),
          currency: this.currency(),
          description: this.description() || null,
          isBillable: this.isBillable(),
          kind: this.kind(),
          onBehalfOfUserId,
        });

    save$.subscribe({
      next: (entry) => onSuccess(entry),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not save the entry.');
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/expenses']);
  }

  protected onAttachmentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.editId) return;

    this.uploadingAttachment.set(true);
    this.attachmentsError.set(null);
    this.expenseEntries.uploadAttachment(Number(this.editId), file).subscribe({
      next: (attachment) => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.attachments.update((list) => [...list, attachment]);
      },
      error: (err) => {
        this.uploadingAttachment.set(false);
        input.value = '';
        this.attachmentsError.set(err?.error?.error ?? 'Could not upload the attachment.');
      },
    });
  }

  protected downloadAttachment(attachment: Attachment): void {
    this.expenseEntries.downloadAttachment(attachment.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.fileName;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  protected async deleteAttachment(attachment: Attachment): Promise<void> {
    const confirmed = await this.confirmService.confirm(`Delete "${attachment.fileName}"?`, {
      confirmLabel: 'Delete',
      destructive: true,
    });
    if (!confirmed) return;

    this.expenseEntries.deleteAttachment(attachment.id).subscribe({
      next: () => this.attachments.update((list) => list.filter((a) => a.id !== attachment.id)),
      error: (err) => this.attachmentsError.set(err?.error?.error ?? 'Could not delete the attachment.'),
    });
  }
}
