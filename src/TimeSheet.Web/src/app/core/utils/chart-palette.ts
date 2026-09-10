/**
 * Small fixed categorical palette for pie/bar chart series - the app's own --success/--warning/--destructive
 * design tokens are only 3-4 colors, not enough to distinguish N staff members or entry-type categories.
 */
export const CHART_PALETTE = [
  '#6366f1',
  '#22c55e',
  '#f59e0b',
  '#ef4444',
  '#06b6d4',
  '#a855f7',
  '#ec4899',
  '#84cc16',
  '#14b8a6',
  '#f97316',
];

export function chartColor(index: number): string {
  return CHART_PALETTE[index % CHART_PALETTE.length];
}
