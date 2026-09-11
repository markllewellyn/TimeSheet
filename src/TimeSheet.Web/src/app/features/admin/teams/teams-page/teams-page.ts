import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TeamsService, Team } from '../../../../core/services/teams.service';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-teams-page',
  standalone: true,
  imports: [FormsModule, LoadingSpinner],
  templateUrl: './teams-page.html',
})
export class TeamsPage {
  private readonly teamsService = inject(TeamsService);
  private readonly confirmService = inject(ConfirmService);

  protected readonly teams = signal<Team[]>([]);
  protected readonly newName = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  // Only ever true->false, on the first refresh() - later refreshes (after add/toggle) don't reset it.
  protected readonly loading = signal(true);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.teamsService.list(true).subscribe({
      next: (teams) => {
        this.teams.set(teams);
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
    this.teamsService.create({ name: this.newName().trim() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.error.set(null);
        this.newName.set('');
        this.refresh();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error ?? 'Could not add the team.');
      },
    });
  }

  protected async toggleActive(team: Team): Promise<void> {
    if (team.isActive) {
      const confirmed = await this.confirmService.confirm(`Deactivate ${team.name}?`, { confirmLabel: 'Deactivate', destructive: true });
      if (!confirmed) return;
    }
    this.teamsService.update(team.id, { name: team.name, isActive: !team.isActive }).subscribe(() => this.refresh());
  }
}
