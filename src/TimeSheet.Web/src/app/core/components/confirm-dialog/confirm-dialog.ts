import { Component, inject } from '@angular/core';
import { ConfirmService } from '../../services/confirm.service';

/** Mounted once in app.html - see ConfirmService for how a page triggers this without its own modal markup. */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: `
    @if (confirmService.request(); as req) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" (click)="respond(false)">
        <div class="card-padded w-full max-w-sm" (click)="$event.stopPropagation()">
          <p class="text-sm text-foreground">{{ req.message }}</p>
          <div class="mt-4 flex justify-end gap-2">
            <button class="btn-secondary" (click)="respond(false)">Cancel</button>
            <button [class]="req.destructive ? 'btn-danger' : 'btn-primary'" (click)="respond(true)">{{ req.confirmLabel }}</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog {
  protected readonly confirmService = inject(ConfirmService);

  protected respond(result: boolean): void {
    this.confirmService.respond(result);
  }
}
