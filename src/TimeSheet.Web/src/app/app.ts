import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { CurrentUserService } from './core/auth/current-user.service';
import { NotificationsService } from './core/services/notifications.service';

@Component({
  imports: [RouterOutlet, RouterLink],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly currentUser = inject(CurrentUserService);
  protected readonly notifications = inject(NotificationsService);
  protected readonly showNotifications = signal(false);
}
