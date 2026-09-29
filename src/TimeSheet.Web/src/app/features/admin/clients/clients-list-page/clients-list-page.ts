import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClientsService } from '../../../../core/services/clients.service';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';
import { Client } from '../../../../core/models/project.models';

@Component({
  selector: 'app-clients-list-page',
  standalone: true,
  imports: [RouterLink, LoadingSpinner],
  templateUrl: './clients-list-page.html',
})
export class ClientsListPage {
  private readonly clientsService = inject(ClientsService);
  private readonly confirmService = inject(ConfirmService);
  protected readonly clients = signal<Client[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.clientsService.list(true).subscribe({
      next: (clients) => {
        this.clients.set(clients);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected async deactivate(client: Client): Promise<void> {
    const confirmed = await this.confirmService.confirm(`Deactivate ${client.name}?`, { confirmLabel: 'Deactivate', destructive: true });
    if (!confirmed) return;
    this.clientsService.deactivate(client.id).subscribe(() => this.refresh());
  }

  protected async reactivate(client: Client): Promise<void> {
    const confirmed = await this.confirmService.confirm(`Reactivate ${client.name}?`, { confirmLabel: 'Reactivate' });
    if (!confirmed) return;
    this.clientsService.reactivate(client.id).subscribe(() => this.refresh());
  }
}
