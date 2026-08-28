import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { RolesService, Role } from '../../../../core/services/roles.service';
import { StaffCostsService } from '../../../../core/services/staff-costs.service';
import { AssignmentsService, StaffAssignment } from '../../../../core/services/assignments.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { AppUser, TenantDirectoryUser } from '../../../../core/models/user.models';
import { StaffCost } from '../../../../core/models/staff-cost.models';
import { Project } from '../../../../core/models/project.models';

@Component({
  selector: 'app-users-list-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './users-list-page.html',
})
export class UsersListPage {
  private readonly usersAdmin = inject(UsersAdminService);
  private readonly rolesService = inject(RolesService);
  private readonly staffCostsService = inject(StaffCostsService);
  private readonly assignmentsService = inject(AssignmentsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly roles = signal<Role[]>([]);
  protected readonly allProjects = signal<Project[]>([]);

  // Filters - FDD: "filterable and searchable, at a minimum on administrator, active or
  // inactive, role and project."
  protected readonly searchText = signal('');
  protected readonly adminsOnly = signal(false);
  protected readonly activeOnly = signal(true);
  protected readonly roleFilter = signal<number | null>(null);
  protected readonly projectFilter = signal<number | null>(null);
  protected readonly projectFilterUserIds = signal<Set<number> | null>(null);

  protected readonly filteredUsers = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    const projectUserIds = this.projectFilterUserIds();
    return this.users().filter((u) => {
      if (search && !u.displayName.toLowerCase().includes(search) && !u.email.toLowerCase().includes(search)) return false;
      if (this.adminsOnly() && u.role !== 'Admin') return false;
      if (this.activeOnly() && !u.isActive) return false;
      if (this.roleFilter() !== null && u.jobRoleId !== this.roleFilter()) return false;
      if (this.projectFilter() !== null && !(projectUserIds?.has(u.id) ?? false)) return false;
      return true;
    });
  });

  protected readonly showInviteForm = signal(false);
  protected readonly accountType = signal<'sso' | 'local'>('sso');
  protected readonly entraObjectId = signal('');
  protected readonly email = signal('');
  protected readonly displayName = signal('');
  protected readonly role = signal<'Admin' | 'User'>('User');
  protected readonly jobRoleId = signal<number | null>(null);
  protected readonly payrollNumber = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly newTemporaryPassword = signal<string | null>(null);

  // Entra directory lookup - FDD: "an administrator can enable or disable any account within the
  // SVG IT tenancy." Search results only ever pre-fill entraObjectId/email/displayName above;
  // Users_Invite itself is unchanged. Explicit search action (not live-as-you-type), matching this
  // app's existing plain-signals style elsewhere.
  protected readonly directorySearchQuery = signal('');
  protected readonly directoryResults = signal<TenantDirectoryUser[]>([]);
  protected readonly directorySearching = signal(false);
  protected readonly directorySearched = signal(false);

  protected readonly editingPayrollUserId = signal<number | null>(null);
  protected readonly editPayrollNumber = signal('');
  protected readonly editSaving = signal(false);

  // A single expanded "Manage" panel per row: cost history + add-cost form, and this person's
  // assignments across every project + an assign-to-project form. Fetched and cached on first
  // expand, same lazy-load approach for both.
  protected readonly expandedUserId = signal<number | null>(null);
  protected readonly costsByUser = signal<Map<number, StaffCost[]>>(new Map());
  protected readonly assignmentsByUser = signal<Map<number, StaffAssignment[]>>(new Map());

  protected readonly newCostHourlyCost = signal<number | null>(null);
  protected readonly newCostOutOfHoursCost = signal<number | null>(null);
  protected readonly newCostEffectiveFrom = signal(new Date().toISOString().slice(0, 10));
  protected readonly costSaving = signal(false);

  protected readonly newAssignmentProjectId = signal<number | null>(null);
  protected readonly newAssignmentStartDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly newAssignmentAllocatedHoursPerWeek = signal<number | null>(null);
  protected readonly assignmentSaving = signal(false);

  constructor() {
    this.refresh();
    this.rolesService.list().subscribe((roles) => this.roles.set(roles));
    this.projectsAdmin.listAll().subscribe((projects) => this.allProjects.set(projects));
  }

  private refresh(): void {
    this.usersAdmin.list().subscribe((users) => this.users.set(users));
  }

  protected onProjectFilterChange(projectId: number | null): void {
    this.projectFilter.set(projectId);
    if (projectId === null) {
      this.projectFilterUserIds.set(null);
      return;
    }
    this.assignmentsService.listByProject(projectId, false).subscribe((assignments) => {
      this.projectFilterUserIds.set(new Set(assignments.map((a) => a.userId)));
    });
  }

  protected searchDirectory(): void {
    const query = this.directorySearchQuery().trim();
    if (query.length < 2) {
      this.error.set('Enter at least 2 characters to search.');
      return;
    }

    this.directorySearching.set(true);
    this.usersAdmin.searchTenantDirectory(query).subscribe({
      next: (results) => {
        this.directorySearching.set(false);
        this.directorySearched.set(true);
        this.error.set(null);
        this.directoryResults.set(results);
      },
      error: (err) => {
        this.directorySearching.set(false);
        this.directorySearched.set(true);
        this.directoryResults.set([]);
        this.error.set(err?.error?.error ?? 'Could not search the tenant directory.');
      },
    });
  }

  protected selectDirectoryResult(result: TenantDirectoryUser): void {
    if (result.isProvisioned) return;
    this.entraObjectId.set(result.entraObjectId);
    this.email.set(result.email ?? '');
    this.displayName.set(result.displayName);
    this.directoryResults.set([]);
    this.directorySearchQuery.set('');
    this.directorySearched.set(false);
  }

  protected invite(): void {
    this.usersAdmin
      .invite({
        entraObjectId: this.accountType() === 'sso' ? this.entraObjectId() : null,
        email: this.email(),
        displayName: this.displayName(),
        role: this.role(),
        jobRoleId: this.jobRoleId(),
        payrollNumber: this.payrollNumber() || null,
      })
      .subscribe({
        next: (result) => {
          this.showInviteForm.set(false);
          this.entraObjectId.set('');
          this.email.set('');
          this.displayName.set('');
          this.jobRoleId.set(null);
          this.payrollNumber.set('');
          this.directorySearchQuery.set('');
          this.directoryResults.set([]);
          this.directorySearched.set(false);
          this.error.set(null);
          this.newTemporaryPassword.set(result.temporaryPassword);
          this.refresh();
        },
        error: (err) => this.error.set(err?.error?.error ?? 'Could not invite the user.'),
      });
  }

  protected toggleActive(user: AppUser): void {
    this.usersAdmin
      .update(user.id, {
        displayName: user.displayName,
        role: user.role,
        jobRoleId: user.jobRoleId,
        isActive: !user.isActive,
        payrollNumber: user.payrollNumber,
      })
      .subscribe(() => this.refresh());
  }

  protected toggleRole(user: AppUser): void {
    const newRole = user.role === 'Admin' ? 'User' : 'Admin';
    this.usersAdmin
      .update(user.id, {
        displayName: user.displayName,
        role: newRole,
        jobRoleId: user.jobRoleId,
        isActive: user.isActive,
        payrollNumber: user.payrollNumber,
      })
      .subscribe(() => this.refresh());
  }

  protected updateJobRole(user: AppUser, jobRoleId: number | null): void {
    this.usersAdmin
      .update(user.id, {
        displayName: user.displayName,
        role: user.role,
        jobRoleId,
        isActive: user.isActive,
        payrollNumber: user.payrollNumber,
      })
      .subscribe(() => this.refresh());
  }

  protected startEditPayroll(user: AppUser): void {
    this.editingPayrollUserId.set(user.id);
    this.editPayrollNumber.set(user.payrollNumber);
  }

  protected cancelEditPayroll(): void {
    this.editingPayrollUserId.set(null);
  }

  protected saveEditPayroll(user: AppUser): void {
    if (!this.editPayrollNumber().trim()) {
      this.error.set('Payroll number is required.');
      return;
    }

    this.editSaving.set(true);
    this.usersAdmin
      .update(user.id, {
        displayName: user.displayName,
        role: user.role,
        jobRoleId: user.jobRoleId,
        isActive: user.isActive,
        payrollNumber: this.editPayrollNumber().trim(),
      })
      .subscribe({
        next: () => {
          this.editSaving.set(false);
          this.error.set(null);
          this.editingPayrollUserId.set(null);
          this.refresh();
        },
        error: (err) => {
          this.editSaving.set(false);
          this.error.set(err?.error?.error ?? 'Could not update the payroll number.');
        },
      });
  }

  protected toggleExpanded(user: AppUser): void {
    if (this.expandedUserId() === user.id) {
      this.expandedUserId.set(null);
      return;
    }
    this.expandedUserId.set(user.id);
    this.newCostHourlyCost.set(null);
    this.newCostOutOfHoursCost.set(null);
    this.newCostEffectiveFrom.set(new Date().toISOString().slice(0, 10));
    this.newAssignmentProjectId.set(null);
    this.newAssignmentStartDate.set(new Date().toISOString().slice(0, 10));
    this.newAssignmentAllocatedHoursPerWeek.set(null);
    this.refreshCosts(user.id);
    this.refreshAssignments(user.id);
  }

  private refreshCosts(userId: number): void {
    this.staffCostsService.listByStaff(userId).subscribe((costs) => {
      const map = new Map(this.costsByUser());
      map.set(userId, costs);
      this.costsByUser.set(map);
    });
  }

  private refreshAssignments(userId: number): void {
    this.assignmentsService.listByUser(userId).subscribe((assignments) => {
      const map = new Map(this.assignmentsByUser());
      map.set(userId, assignments);
      this.assignmentsByUser.set(map);
    });
  }

  protected saveAddCost(user: AppUser): void {
    if (this.newCostHourlyCost() === null || this.newCostOutOfHoursCost() === null || !this.newCostEffectiveFrom()) {
      this.error.set('Enter both costs and an effective date.');
      return;
    }

    this.costSaving.set(true);
    this.staffCostsService
      .create({
        staffId: user.id,
        hourlyCost: this.newCostHourlyCost()!,
        outOfHoursCost: this.newCostOutOfHoursCost()!,
        effectiveFrom: this.newCostEffectiveFrom(),
      })
      .subscribe({
        next: () => {
          this.costSaving.set(false);
          this.error.set(null);
          this.newCostHourlyCost.set(null);
          this.newCostOutOfHoursCost.set(null);
          this.refreshCosts(user.id);
        },
        error: (err) => {
          this.costSaving.set(false);
          this.error.set(err?.error?.error ?? 'Could not add the cost.');
        },
      });
  }

  protected saveAssignment(user: AppUser): void {
    const projectId = this.newAssignmentProjectId();
    if (!projectId) {
      this.error.set('Please select a project.');
      return;
    }

    this.assignmentSaving.set(true);
    this.assignmentsService
      .create({
        projectId,
        userId: user.id,
        startDate: this.newAssignmentStartDate(),
        allocatedHoursPerWeek: this.newAssignmentAllocatedHoursPerWeek(),
        notes: null,
      })
      .subscribe({
        next: () => {
          this.assignmentSaving.set(false);
          this.error.set(null);
          this.newAssignmentProjectId.set(null);
          this.newAssignmentAllocatedHoursPerWeek.set(null);
          this.refreshAssignments(user.id);
        },
        error: (err) => {
          this.assignmentSaving.set(false);
          this.error.set(err?.error?.error ?? 'Could not assign this project.');
        },
      });
  }

  protected endAssignment(user: AppUser, assignment: StaffAssignment): void {
    this.assignmentsService
      .update(assignment.id, {
        status: 'Ended',
        endDate: new Date().toISOString().slice(0, 10),
        allocatedHoursPerWeek: assignment.allocatedHoursPerWeek,
        notes: assignment.notes,
      })
      .subscribe(() => this.refreshAssignments(user.id));
  }

  protected resetPassword(user: AppUser): void {
    const target = user.isLocalAccount ? 'in this app' : 'in Entra ID';
    if (!confirm(`Force a password reset for ${user.displayName} ${target}?`)) return;
    this.usersAdmin.resetPassword(user.id).subscribe({
      next: (result) => this.newTemporaryPassword.set(result.temporaryPassword),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not reset the password.'),
    });
  }
}
