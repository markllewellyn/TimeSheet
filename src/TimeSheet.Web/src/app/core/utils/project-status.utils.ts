/**
 * Shared "is this on track" color banding for a used-percent figure (hours-of-budget or cost-of-fixed-fee) -
 * used by both the compact list badges and the Project edit page's fuller status panel, so the two never
 * silently disagree on what counts as "amber". Deliberately its own fixed 75/100 banding, independent of the
 * Settings-configurable ProjectBudgetWarningThresholdPercent/ProjectBudgetAlertThresholdPercent (those drive
 * when a notification fires, not this purely visual indicator).
 */
export function projectStatusBadgeClass(percent: number): string {
  if (percent >= 100) return 'badge-danger';
  if (percent >= 75) return 'badge-warning';
  return 'badge-success';
}

/** Same banding as projectStatusBadgeClass, as a solid CSS color for a progress-bar fill rather than a badge's
 * tinted background - reuses the exact same design tokens the badge classes are already built from. */
export function projectStatusBarColor(percent: number): string {
  if (percent >= 100) return 'var(--destructive)';
  if (percent >= 75) return 'var(--warning)';
  return 'var(--success)';
}
