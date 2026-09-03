import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AssignmentsService, ProjectAssignment } from '../../../../core/services/assignments.service';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { AppUser } from '../../../../core/models/user.models';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-project-assignments-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
  templateUrl: './project-assignments-page.html',
})
export class ProjectAssignmentsPage {
  private readonly assignmentsService = inject(AssignmentsService);
  private readonly usersAdmin = inject(UsersAdminService);
  private readonly route = inject(ActivatedRoute);

  protected readonly projectId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly assignments = signal<ProjectAssignment[]>([]);
  protected readonly users = signal<AppUser[]>([]);
  // Only ever true->false, on the first refresh() - subsequent refreshes don't reset it.
  protected readonly loading = signal(true);

  protected readonly newUserId = signal<number | null>(null);
  protected readonly newStartDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly newAllocatedHoursPerWeek = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.refresh();
    this.usersAdmin.list(false).subscribe((users) => this.users.set(users));
  }

  private refresh(): void {
    this.assignmentsService.listByProject(this.projectId).subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected addAssignment(): void {
    const userId = this.newUserId();
    if (!userId) {
      this.error.set('Please select a user.');
      return;
    }

    this.saving.set(true);
    this.assignmentsService
      .create({ projectId: this.projectId, userId, startDate: this.newStartDate(), allocatedHoursPerWeek: this.newAllocatedHoursPerWeek(), notes: null })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.error.set(null);
          this.newUserId.set(null);
          this.newAllocatedHoursPerWeek.set(null);
          this.refresh();
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? 'Could not add the assignment.');
        },
      });
  }

  protected endAssignment(assignment: ProjectAssignment): void {
    this.assignmentsService
      .update(assignment.id, {
        status: 'Ended',
        endDate: new Date().toISOString().slice(0, 10),
        allocatedHoursPerWeek: assignment.allocatedHoursPerWeek,
        notes: assignment.notes,
      })
      .subscribe(() => this.refresh());
  }
}
