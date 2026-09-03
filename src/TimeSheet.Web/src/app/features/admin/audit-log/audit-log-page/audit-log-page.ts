import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditLogService, AuditLogEntry } from '../../../../core/services/audit-log.service';
import { UsersAdminService } from '../../../../core/services/users-admin.service';
import { AppUser } from '../../../../core/models/user.models';
import { LoadingSpinner } from '../../../../core/components/loading-spinner/loading-spinner';

@Component({
  selector: 'app-audit-log-page',
  standalone: true,
  imports: [DatePipe, FormsModule, LoadingSpinner],
  templateUrl: './audit-log-page.html',
})
export class AuditLogPage {
  private readonly auditLogService = inject(AuditLogService);
  private readonly usersAdmin = inject(UsersAdminService);
  protected readonly logs = signal<AuditLogEntry[]>([]);
  protected readonly users = signal<AppUser[]>([]);
  // Only ever true->false, on the first refresh() - subsequent filter changes don't reset it.
  protected readonly loading = signal(true);

  protected readonly userIdFilter = signal<number | null>(null);
  protected readonly from = signal('');
  protected readonly to = signal('');

  constructor() {
    this.usersAdmin.list(true).subscribe((users) => this.users.set(users));
    this.refresh();
  }

  protected refresh(): void {
    this.auditLogService
      .list({ userId: this.userIdFilter(), from: this.from() || null, to: this.to() || null })
      .subscribe({
        next: (logs) => {
          this.logs.set(logs);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
