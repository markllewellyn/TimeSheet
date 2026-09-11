import { ProjectEstimateLine } from '../models/project.models';

export interface EstimateRoleGroup {
  roleId: number | null;
  roleName: string;
  allocatedHours: number;
  estimatedCost: number;
  estimatedRevenue: number;
  estimatedProfit: number;
}

/**
 * Roll the Estimated Cost/Profit panel's per-user lines up by role, per the FDD's "calculated per user and per
 * role" requirement - the per-user table already existed, this just adds the missing role-level subtotal.
 * A line with a rate-resolution warning already carries cost/revenue/profit of 0 (set in
 * ProjectEstimateService), so it sums in safely without special-casing. Unrolled (no RoleId) lines group under
 * "Unspecified" rather than being dropped, so the rollup's totals still match the per-user table's totals.
 */
export function groupEstimateLinesByRole(lines: ProjectEstimateLine[]): EstimateRoleGroup[] {
  const byRole = new Map<string, EstimateRoleGroup>();
  for (const line of lines) {
    const key = line.roleId === null ? 'none' : String(line.roleId);
    const existing = byRole.get(key);
    if (existing) {
      existing.allocatedHours += line.allocatedHours;
      existing.estimatedCost += line.estimatedCost;
      existing.estimatedRevenue += line.estimatedRevenue;
      existing.estimatedProfit += line.estimatedProfit;
    } else {
      byRole.set(key, {
        roleId: line.roleId,
        roleName: line.roleName ?? 'Unspecified',
        allocatedHours: line.allocatedHours,
        estimatedCost: line.estimatedCost,
        estimatedRevenue: line.estimatedRevenue,
        estimatedProfit: line.estimatedProfit,
      });
    }
  }
  return [...byRole.values()].sort((a, b) => a.roleName.localeCompare(b.roleName));
}
