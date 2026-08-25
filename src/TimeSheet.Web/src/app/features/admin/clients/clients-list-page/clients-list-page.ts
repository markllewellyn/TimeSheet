import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ClientsService } from '../../../../core/services/clients.service';
import { Client } from '../../../../core/models/project.models';

@Component({
  selector: 'app-clients-list-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './clients-list-page.html',
})
export class ClientsListPage {
  private readonly clientsService = inject(ClientsService);
  protected readonly clients = signal<Client[]>([]);

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.clientsService.list(true).subscribe((clients) => this.clients.set(clients));
  }

  protected deactivate(client: Client): void {
    if (!confirm(`Deactivate ${client.name}?`)) return;
    this.clientsService.deactivate(client.id).subscribe(() => this.refresh());
  }
}
