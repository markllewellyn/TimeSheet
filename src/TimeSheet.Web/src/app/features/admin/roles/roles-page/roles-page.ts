import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RolesService, Role } from '../../../../core/services/roles.service';

@Component({
  selector: 'app-roles-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './roles-page.html',
})
export class RolesPage {
  private readonly rolesService = inject(RolesService);

  protected readonly roles = signal<Role[]>([]);
  protected readonly newName = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.rolesService.list(true).subscribe((roles) => this.roles.set(roles));
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

  protected toggleActive(role: Role): void {
    this.rolesService.update(role.id, { name: role.name, isActive: !role.isActive }).subscribe(() => this.refresh());
  }
}
