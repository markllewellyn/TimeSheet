import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RolesService, Role } from '../../../../core/services/roles.service';
import { RateCardsService, RateCard } from '../../../../core/services/rate-cards.service';
import { ClientsService } from '../../../../core/services/clients.service';
import { ProjectsAdminService } from '../../../../core/services/projects-admin.service';
import { Client, Project } from '../../../../core/models/project.models';

type ScopeType = 'default' | 'client' | 'project';

@Component({
  selector: 'app-rate-cards-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './rate-cards-page.html',
})
export class RateCardsPage {
  private readonly rolesService = inject(RolesService);
  private readonly rateCardsService = inject(RateCardsService);
  private readonly clientsService = inject(ClientsService);
  private readonly projectsAdmin = inject(ProjectsAdminService);

  protected readonly roles = signal<Role[]>([]);
  protected readonly clients = signal<Client[]>([]);
  protected readonly projectsForClient = signal<Project[]>([]);
  protected readonly rateCards = signal<RateCard[]>([]);

  protected readonly selectedRoleId = signal<number | null>(null);
  protected readonly scopeType = signal<ScopeType>('default');
  protected readonly scopeClientId = signal<number | null>(null);
  protected readonly scopeProjectId = signal<number | null>(null);
  protected readonly rate = signal<number | null>(null);
  protected readonly discountPercent = signal<number | null>(null);
  protected readonly effectiveFrom = signal(new Date().toISOString().slice(0, 10));

  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.rolesService.list().subscribe((roles) => this.roles.set(roles));
    this.clientsService.list().subscribe((clients) => this.clients.set(clients));
  }

  protected onRoleChange(roleId: number): void {
    this.selectedRoleId.set(roleId);
    this.rateCardsService.list({ roleId }).subscribe((cards) => this.rateCards.set(cards));
  }

  protected onScopeClientChange(clientId: number): void {
    this.scopeClientId.set(clientId);
    this.scopeProjectId.set(null);
    this.projectsAdmin.listByClient(clientId).subscribe((projects) => this.projectsForClient.set(projects));
  }

  protected add(): void {
    const roleId = this.selectedRoleId();
    if (!roleId || this.rate() === null || !this.effectiveFrom()) {
      this.error.set('Select a role, enter a rate and an effective date.');
      return;
    }
    if (this.scopeType() === 'client' && !this.scopeClientId()) {
      this.error.set('Select a client.');
      return;
    }
    if (this.scopeType() === 'project' && !this.scopeProjectId()) {
      this.error.set('Select a project.');
      return;
    }

    this.saving.set(true);
    this.rateCardsService
      .create({
        roleId,
        clientId: this.scopeType() === 'client' ? this.scopeClientId() : null,
        projectId: this.scopeType() === 'project' ? this.scopeProjectId() : null,
        rate: this.rate()!,
        discountPercent: this.discountPercent(),
        effectiveFrom: this.effectiveFrom(),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.error.set(null);
          this.rate.set(null);
          this.discountPercent.set(null);
          this.onRoleChange(roleId);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(err?.error?.error ?? 'Could not add the rate card.');
        },
      });
  }
}
