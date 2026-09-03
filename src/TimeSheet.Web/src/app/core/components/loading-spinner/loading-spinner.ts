import { Component, input } from '@angular/core';

/** Shared initial-load indicator - `@if (loading()) { <app-loading-spinner /> } @else { ...content }` is the
 * convention used across page components, so every page shows the same thing during its first data fetch
 * instead of a blank flash or (worse) a briefly-false "no data" empty-state. */
@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  template: `
    <div class="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
      <svg class="h-5 w-5 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" aria-hidden="true">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4z"></path>
      </svg>
      <span>{{ label() }}</span>
    </div>
  `,
})
export class LoadingSpinner {
  label = input('Loading…');
}
