import { InvoiceLineItem } from '../services/invoices.service';

export interface InvoiceLineItemGroup {
  projectId: number;
  projectName: string;
  lines: InvoiceLineItem[];
}

/**
 * Group an invoice's per-entry line items by project - one heading + table per project, rather than a flat
 * list mixing every project's entries together, now that generation produces one line per entry instead of
 * one per project. Sorted by project name, then by task date within each project. Shared by the admin
 * invoicing page and the read-only My Invoices (PM) page.
 */
export function groupInvoiceLineItemsByProject(lineItems: InvoiceLineItem[]): InvoiceLineItemGroup[] {
  const byProject = new Map<number, InvoiceLineItem[]>();
  for (const line of lineItems) {
    const group = byProject.get(line.projectId);
    if (group) group.push(line);
    else byProject.set(line.projectId, [line]);
  }
  return [...byProject.entries()]
    .map(([projectId, lines]) => ({
      projectId,
      projectName: lines[0].projectName,
      lines: [...lines].sort((a, b) => (a.taskDate ?? '').localeCompare(b.taskDate ?? '')),
    }))
    .sort((a, b) => a.projectName.localeCompare(b.projectName));
}
