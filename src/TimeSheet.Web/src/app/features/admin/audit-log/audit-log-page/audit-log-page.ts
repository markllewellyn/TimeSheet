import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditLogService, AuditLogEntry } from '../../../../core/services/audit-log.service';

@Component({
  selector: 'app-audit-log-page',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './audit-log-page.html',
})
export class AuditLogPage {
  private readonly auditLogService = inject(AuditLogService);
  protected readonly logs = signal<AuditLogEntry[]>([]);

  protected readonly userIdFilter = signal<number | null>(null);
  protected readonly from = signal('');
  protected readonly to = signal('');

  constructor() {
    this.refresh();
  }

  protected refresh(): void {
    this.auditLogService
      .list({ userId: this.userIdFilter(), from: this.from() || null, to: this.to() || null })
      .subscribe((logs) => this.logs.set(logs));
  }
}
