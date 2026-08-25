import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { AppUser } from '../../../../core/models/user.models';

@Component({
  selector: 'app-users-list-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './users-list-page.html',
})
export class UsersListPage {
  private readonly usersAdmin = inject(UsersAdminService);
  protected readonly users = signal<AppUser[]>([]);

  protected readonly showInviteForm = signal(false);
  protected readonly entraObjectId = signal('');
  protected readonly email = signal('');
  protected readonly displayName = signal('');
  protected readonly role = signal<'Admin' | 'User'>('User');
  protected readonly jobTitle = signal('');
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.usersAdmin.list().subscribe((users) => this.users.set(users));
  }

  protected invite(): void {
    this.usersAdmin
      .invite({
        entraObjectId: this.entraObjectId(),
        email: this.email(),
        displayName: this.displayName(),
        role: this.role(),
        jobTitle: this.jobTitle() || null,
      })
      .subscribe({
        next: () => {
          this.showInviteForm.set(false);
          this.entraObjectId.set('');
          this.email.set('');
          this.displayName.set('');
          this.jobTitle.set('');
          this.error.set(null);
          this.refresh();
        },
        error: (err) => this.error.set(err?.error?.error ?? 'Could not invite the user.'),
      });
  }

  protected toggleActive(user: AppUser): void {
    this.usersAdmin
      .update(user.id, { displayName: user.displayName, role: user.role, jobTitle: user.jobTitle, isActive: !user.isActive })
      .subscribe(() => this.refresh());
  }

  protected toggleRole(user: AppUser): void {
    const newRole = user.role === 'Admin' ? 'User' : 'Admin';
    this.usersAdmin
      .update(user.id, { displayName: user.displayName, role: newRole, jobTitle: user.jobTitle, isActive: user.isActive })
      .subscribe(() => this.refresh());
  }

  protected resetPassword(user: AppUser): void {
    if (!confirm(`Force a password reset for ${user.displayName} in Entra ID?`)) return;
    this.usersAdmin.resetPassword(user.id).subscribe({
      next: (result) => alert(`Temporary password for ${user.displayName}:\n\n${result.temporaryPassword}\n\nThey'll be required to change it at next sign-in.`),
      error: (err) => this.error.set(err?.error?.error ?? 'Could not reset the password.'),
    });
  }
}
