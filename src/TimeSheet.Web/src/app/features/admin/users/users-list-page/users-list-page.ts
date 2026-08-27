import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { RolesService, Role } from '../../../../core/services/roles.service';
import { StaffCostsService } from '../../../../core/services/staff-costs.service';
import { AppUser } from '../../../../core/models/user.models';

@Component({
  selector: 'app-users-list-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './users-list-page.html',
})
export class UsersListPage {
  private readonly usersAdmin = inject(UsersAdminService);
  private readonly rolesService = inject(RolesService);
  private readonly staffCostsService = inject(StaffCostsService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly roles = signal<Role[]>([]);

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

  protected readonly editingPayrollUserId = signal<number | null>(null);
  protected readonly editPayrollNumber = signal('');
  protected readonly editSaving = signal(false);

  // A person's effective-dated internal cost - "adding" always inserts a new dated row, never edits one.
  protected readonly addingCostUserId = signal<number | null>(null);
  protected readonly newCostHourlyCost = signal<number | null>(null);
  protected readonly newCostOutOfHoursCost = signal<number | null>(null);
  protected readonly newCostEffectiveFrom = signal(new Date().toISOString().slice(0, 10));
  protected readonly costSaving = signal(false);

  constructor() {
    this.refresh();
    this.rolesService.list().subscribe((roles) => this.roles.set(roles));
  }

  private refresh(): void {
    this.usersAdmin.list().subscribe((users) => this.users.set(users));
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

  protected startAddCost(user: AppUser): void {
    this.addingCostUserId.set(user.id);
    this.newCostHourlyCost.set(null);
    this.newCostOutOfHoursCost.set(null);
    this.newCostEffectiveFrom.set(new Date().toISOString().slice(0, 10));
  }

  protected cancelAddCost(): void {
    this.addingCostUserId.set(null);
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
          this.addingCostUserId.set(null);
        },
        error: (err) => {
          this.costSaving.set(false);
          this.error.set(err?.error?.error ?? 'Could not add the cost.');
        },
      });
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
