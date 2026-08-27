import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TimesheetEntriesService } from '../../../core/services/timesheet-entries.service';
import { TimesheetEntry } from '../../../core/models/timesheet-entry.models';
import { ImpersonationService } from '../../../core/services/impersonation.service';

// A small fixed palette, indexed deterministically by clientId (see colorForClient) - avoids hardcoding
// specific client names to specific colors (the legacy app did this, which breaks the moment a client is
// renamed or a new one is added). Chosen to read reasonably against both light and dark card backgrounds.
const CLIENT_COLORS = ['#6366F1', '#0EA5E9', '#F59E0B', '#10B981', '#EC4899', '#8B5CF6', '#EF4444', '#14B8A6'];

function toIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

interface CalendarDay {
  date: string;
  inMonth: boolean;
  isToday: boolean;
}

@Component({
  selector: 'app-calendar-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './calendar-page.html',
})
export class CalendarPage {
  private readonly timesheetEntries = inject(TimesheetEntriesService);
  private readonly impersonation = inject(ImpersonationService);

  private readonly today = new Date();
  protected readonly viewYear = signal(this.today.getFullYear());
  protected readonly viewMonth = signal(this.today.getMonth() + 1); // 1-12

  protected readonly entries = signal<TimesheetEntry[]>([]);
  protected readonly selectedDate = signal<string | null>(null);
  protected readonly clientFilter = signal<number | null>(null);

  protected readonly monthLabel = computed(() =>
    new Date(this.viewYear(), this.viewMonth() - 1, 1).toLocaleDateString(undefined, { month: 'long', year: 'numeric' }),
  );

  private readonly viewRange = computed(() => {
    const firstOfMonth = new Date(this.viewYear(), this.viewMonth() - 1, 1);
    const lastOfMonth = new Date(this.viewYear(), this.viewMonth(), 0);
    const mondayOffset = (firstOfMonth.getDay() + 6) % 7; // 0 = Monday
    const firstInView = new Date(firstOfMonth);
    firstInView.setDate(firstInView.getDate() - mondayOffset);
    const lastInView = new Date(firstInView);
    lastInView.setDate(lastInView.getDate() + 41); // 6 weeks
    return { firstOfMonth, lastOfMonth, firstInView, lastInView };
  });

  protected readonly gridDays = computed<CalendarDay[]>(() => {
    const { firstInView } = this.viewRange();
    const todayIso = toIso(this.today);
    const days: CalendarDay[] = [];
    for (let i = 0; i < 42; i++) {
      const d = new Date(firstInView);
      d.setDate(d.getDate() + i);
      const iso = toIso(d);
      days.push({ date: iso, inMonth: d.getMonth() === this.viewMonth() - 1, isToday: iso === todayIso });
    }
    return days;
  });

  protected readonly distinctClients = computed(() => {
    const seen = new Map<number, string>();
    for (const e of this.entries()) seen.set(e.clientId, e.clientName);
    return Array.from(seen, ([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly filteredEntries = computed(() => {
    const clientId = this.clientFilter();
    return clientId === null ? this.entries() : this.entries().filter((e) => e.clientId === clientId);
  });

  protected readonly entriesByDate = computed(() => {
    const map = new Map<string, TimesheetEntry[]>();
    for (const e of this.filteredEntries()) {
      const list = map.get(e.date) ?? [];
      list.push(e);
      map.set(e.date, list);
    }
    return map;
  });

  protected readonly clientsByDate = computed(() => {
    const map = new Map<string, { clientId: number; clientName: string; hours: number }[]>();
    for (const [date, dayEntries] of this.entriesByDate()) {
      const byClient = new Map<number, { clientId: number; clientName: string; hours: number }>();
      for (const e of dayEntries) {
        const existing = byClient.get(e.clientId);
        const hours = e.workHours + e.outOfHoursHours;
        if (existing) existing.hours += hours;
        else byClient.set(e.clientId, { clientId: e.clientId, clientName: e.clientName, hours });
      }
      map.set(date, Array.from(byClient.values()));
    }
    return map;
  });

  protected readonly monthTotal = computed(() => {
    const { firstOfMonth, lastOfMonth } = this.viewRange();
    const firstIso = toIso(firstOfMonth);
    const lastIso = toIso(lastOfMonth);
    return this.filteredEntries()
      .filter((e) => e.date >= firstIso && e.date <= lastIso)
      .reduce((s, e) => s + e.workHours + e.outOfHoursHours, 0);
  });

  protected readonly selectedDayEntries = computed(() => {
    const date = this.selectedDate();
    return date ? (this.entriesByDate().get(date) ?? []) : [];
  });

  constructor() {
    this.refresh();
  }

  protected prevMonth(): void {
    this.shiftMonth(-1);
  }

  protected nextMonth(): void {
    this.shiftMonth(1);
  }

  private shiftMonth(delta: number): void {
    let month = this.viewMonth() + delta;
    let year = this.viewYear();
    if (month < 1) {
      month = 12;
      year -= 1;
    } else if (month > 12) {
      month = 1;
      year += 1;
    }
    this.viewYear.set(year);
    this.viewMonth.set(month);
    this.selectedDate.set(null);
    this.refresh();
  }

  protected selectDay(date: string): void {
    this.selectedDate.set(this.selectedDate() === date ? null : date);
  }

  protected colorForClient(clientId: number): string {
    return CLIENT_COLORS[Math.abs(clientId) % CLIENT_COLORS.length];
  }

  private refresh(): void {
    const { firstInView, lastInView } = this.viewRange();
    this.timesheetEntries
      .list({ from: toIso(firstInView), to: toIso(lastInView), onBehalfOfUserId: this.impersonation.actingAs()?.id })
      .subscribe((res) => {
        this.entries.set(res.entries);
      });
  }
}
