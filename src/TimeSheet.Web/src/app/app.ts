import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { CurrentUserService } from './core/auth/current-user.service';
import { NotificationsService } from './core/services/notifications.service';
import { ThemeService } from './core/services/theme.service';
import { ImpersonationService } from './core/services/impersonation.service';
import { UsersAdminService } from './core/services/users-admin.service';
import { AppUser } from './core/models/user.models';

// The routes tucked inside the "Admin" nav dropdown - used to highlight the dropdown's trigger button whenever
// the current page is one of these, even while the dropdown itself is closed (see isAdminSectionActive below).
const ADMIN_MENU_PATHS = ['/admin/audit-log', '/admin/clients', '/admin/projects', '/admin/rate-cards', '/admin/roles', '/admin/settings', '/admin/users'];

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
  private readonly router = inject(Router);

  protected readonly showNotifications = signal(false);
  protected readonly showImpersonationPicker = signal(false);
  protected readonly showAdminMenu = signal(false);
  protected readonly impersonableUsers = signal<AppUser[]>([]);

  // The "Admin" dropdown's <a> tags are unmounted (via @if) while closed, so a plain routerLinkActive on the
  // trigger can't see them to know if one is the current route - this tracks the URL directly instead.
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly isAdminSectionActive = computed(() => ADMIN_MENU_PATHS.some((path) => this.currentUrl().startsWith(path)));

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('[data-dropdown="admin"]')) this.showAdminMenu.set(false);
    if (!target.closest('[data-dropdown="notifications"]')) this.showNotifications.set(false);
    if (!target.closest('[data-dropdown="impersonation"]')) this.showImpersonationPicker.set(false);
  }

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
