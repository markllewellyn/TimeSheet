import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { projectStatusBadgeClass } from '../../utils/project-status.utils';

/**
 * Compact "is this project on track" indicator for a list row - up to two badges (Hours-of-budget,
 * Cost-of-fixed-fee), whichever the project actually has a figure to compare against; a single neutral badge
 * when neither BudgetHours nor FixedFeeAmount is set. See project-status.utils for the shared color banding.
 */
@Component({
  selector: 'app-project-status-badges',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    <div class="flex flex-wrap gap-1">
      @if (hoursUsedPercent() === null || hoursUsedPercent() === undefined) {
        @if (costUsedPercent() === null || costUsedPercent() === undefined) {
          <span class="badge-neutral">No budget set</span>
        }
      } @else {
        <span [class]="badgeClass(hoursUsedPercent()!)" title="Actual hours vs Budget Hours">
          Hours {{ hoursUsedPercent()! | number: '1.0-0' }}%
        </span>
      }
      @if (costUsedPercent(); as costPercent) {
        <span [class]="badgeClass(costPercent)" title="Actual cost vs Fixed Fee Amount">Cost {{ costPercent | number: '1.0-0' }}%</span>
      }
    </div>
  `,
})
export class ProjectStatusBadges {
  hoursUsedPercent = input<number | null | undefined>(undefined);
  costUsedPercent = input<number | null | undefined>(undefined);

  protected badgeClass(percent: number): string {
    return projectStatusBadgeClass(percent);
  }
}
