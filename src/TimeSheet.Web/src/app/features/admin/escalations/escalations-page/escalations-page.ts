import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { EscalationsService, Escalation } from '../../../../core/services/escalations.service';

@Component({
  selector: 'app-escalations-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './escalations-page.html',
})
export class EscalationsPage {
  private readonly escalationsService = inject(EscalationsService);
  protected readonly escalations = signal<Escalation[]>([]);

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.escalationsService.listPending().subscribe((escalations) => this.escalations.set(escalations));
  }

  protected approve(e: Escalation): void {
    const notes = prompt('Approval notes (optional):');
    this.escalationsService.approve(e.id, notes || null).subscribe(() => this.refresh());
  }

  protected decline(e: Escalation): void {
    const notes = prompt('Decline notes (optional):');
    this.escalationsService.decline(e.id, notes || null).subscribe(() => this.refresh());
  }
}
