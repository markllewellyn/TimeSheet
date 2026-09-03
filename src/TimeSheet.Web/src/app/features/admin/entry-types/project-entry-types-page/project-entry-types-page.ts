import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { EntryTypesService, EntryType } from '../../../../core/services/entry-types.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-project-entry-types-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
  templateUrl: './project-entry-types-page.html',
})
export class ProjectEntryTypesPage {
  private readonly entryTypesService = inject(EntryTypesService);
  private readonly projectsAdmin = inject(ProjectsAdminService);
  private readonly route = inject(ActivatedRoute);

  protected readonly projectId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly entryTypes = signal<EntryType[]>([]);
  protected readonly isContractProject = signal(false);
  // Only ever true->false, on the first refresh() - subsequent refreshes don't reset it.
  protected readonly loading = signal(true);

  protected readonly newName = signal('');
  protected readonly newIsContractType = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.refresh();
    this.projectsAdmin.getById(this.projectId).subscribe((p) => this.isContractProject.set(p.projectType === 'Contract'));
  }

  private refresh(): void {
    this.entryTypesService.listByProject(this.projectId).subscribe({
      next: (entryTypes) => {
        this.entryTypes.set(entryTypes);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected addEntryType(): void {
    if (!this.newName().trim()) {
      this.error.set('Please enter a name.');
      return;
    }

    this.saving.set(true);
    this.entryTypesService.create(this.projectId, { name: this.newName().trim(), isContractType: this.newIsContractType() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.error.set(null);
        this.newName.set('');
        this.newIsContractType.set(false);
        this.refresh();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not add the entry type.');
      },
    });
  }

  protected toggleActive(entryType: EntryType): void {
    this.entryTypesService.update(entryType.id, { name: entryType.name, isActive: !entryType.isActive }).subscribe(() => this.refresh());
  }
}
