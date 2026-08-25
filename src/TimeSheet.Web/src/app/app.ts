import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CurrentUserService } from './core/auth/current-user.service';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly currentUser = inject(CurrentUserService);
}
