import { Component } from '@angular/core';
import { ICellRendererAngularComp } from 'ag-grid-angular';
import { ICellRendererParams } from 'ag-grid-community';
import { TimesheetEntry } from '../../../core/models/timesheet-entry.models';

export interface EntryActionsContext {
  onEdit: (entry: TimesheetEntry) => void;
  onDuplicate: (entry: TimesheetEntry) => void;
  onDelete: (entry: TimesheetEntry) => void;
}

@Component({
  selector: 'app-entry-actions-cell',
  standalone: true,
  template: `
    <div class="flex h-full items-center gap-3 text-sm">
      @if (locked()) {
        <span class="text-xs text-muted-foreground">{{ lockedReason() }}</span>
      } @else {
        <button class="hover:underline" (click)="edit()">Edit</button>
        <button class="hover:underline" (click)="duplicate()">Duplicate</button>
        <button class="text-red-600 hover:underline" (click)="delete()">Delete</button>
      }
    </div>
  `,
})
export class EntryActionsCell implements ICellRendererAngularComp {
  private params!: ICellRendererParams<TimesheetEntry> & EntryActionsContext;

  agInit(params: ICellRendererParams<TimesheetEntry> & EntryActionsContext): void {
    this.params = params;
  }

  // Mirrors the server's lock rule (TimesheetEntriesFunctions Update/Delete/Duplicate): approval alone locks
  // an entry, not only sending it to payroll.
  locked(): boolean {
    const e = this.params.data;
    return e?.approvedPayroll === true || e?.sentToPayroll === true || e?.invoiced === true;
  }

  lockedReason(): string {
    const e = this.params.data;
    if (e?.invoiced === true) return 'Locked (invoiced)';
    if (e?.sentToPayroll === true) return 'Locked (sent to payroll)';
    return 'Locked (approved)';
  }

  refresh(): boolean {
    return false;
  }

  edit(): void {
    if (this.params.data) this.params.onEdit(this.params.data);
  }

  duplicate(): void {
    if (this.params.data) this.params.onDuplicate(this.params.data);
  }

  delete(): void {
    if (this.params.data) this.params.onDelete(this.params.data);
  }
}
