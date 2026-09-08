import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RolesService, Role } from '../../../../core/services/roles.service';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-roles-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
  templateUrl: './roles-page.html',
})
export class RolesPage {
  private readonly rolesService = inject(RolesService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly roles = signal<Role[]>([]);
  protected readonly newName = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  // Only ever true->false, on the first refresh() - later refreshes (after add/toggle) don't reset it.
  protected readonly loading = signal(true);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.rolesService.list(true).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected add(): void {
    if (!this.newName().trim()) {
      this.error.set('Name is required.');
      return;
    }

    this.saving.set(true);
    this.rolesService.create({ name: this.newName().trim() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.error.set(null);
        this.newName.set('');
        this.refresh();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not add the role.');
      },
    });
  }

  protected async toggleActive(role: Role): Promise<void> {
    if (role.isActive) {
      const confirmed = await this.confirmService.confirm(`Deactivate ${role.name}?`, { confirmLabel: 'Deactivate', destructive: true });
      if (!confirmed) return;
    }
    this.rolesService.update(role.id, { name: role.name, isActive: !role.isActive }).subscribe(() => this.refresh());
  }
}
