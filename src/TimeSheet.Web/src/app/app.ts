import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CurrentUserService } from './core/auth/current-user.service';
import { NotificationsService } from './core/services/notifications.service';
import { ThemeService } from './core/services/theme.service';
import { ImpersonationService } from './core/services/impersonation.service';
import { UsersAdminService } from './core/services/users-admin.service';
import { AppUser } from './core/models/user.models';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly currentUser = inject(CurrentUserService);
  protected readonly notifications = inject(NotificationsService);
  protected readonly theme = inject(ThemeService);
  protected readonly impersonation = inject(ImpersonationService);
  private readonly usersAdmin = inject(UsersAdminService);

  protected readonly showNotifications = signal(false);
  protected readonly showImpersonationPicker = signal(false);
  protected readonly impersonableUsers = signal<AppUser[]>([]);

  protected toggleImpersonationPicker(): void {
    if (!this.showImpersonationPicker() && this.impersonableUsers().length === 0) {
      this.usersAdmin.list(false).subscribe((users) =>
        this.impersonableUsers.set(users.filter((u) => u.id !== this.currentUser.me()?.id)),
      );
    }
    this.showImpersonationPicker.set(!this.showImpersonationPicker());
  }

  protected startImpersonating(userId: number): void {
    const user = this.impersonableUsers().find((u) => u.id === userId);
    if (!user) return;
    this.impersonation.start({ id: user.id, displayName: user.displayName });
    this.showImpersonationPicker.set(false);
  }

  protected stopImpersonating(): void {
    this.impersonation.stop();
  }
}
