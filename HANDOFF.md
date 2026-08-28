# TimeSheet — FDD Alignment Handoff (as of 2026-08-28)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done this session (commits, newest first)

- `1d8f627` — Estimated cost/profit projection per project (`IProjectEstimateService`), computed on read from assigned staff × resolved rate/cost, BudgetHours split weighted by `StaffProject.AllocatedHoursPerWeek` (equal split fallback). Shown on the Project edit page.
- `5fd4f96` — Percentage discounts: `RateCard.DiscountPercent` (baked into `IRateResolver`'s resolved rate, since invoicing never re-resolves) and `InvoiceLineItem.DiscountPercent`/`GrossAmount` (settable only while an invoice is Draft, via `Invoices_ApplyLineItemDiscount`).
- `3a4ebf9` — Per-project `EntryType` (admin-defined list, `IsContractType` gated by the new `Project.ProjectType`), wired onto `TimesheetEntry.EntryTypeId` (optional — see deviation note below).
- `d6d1d1d` — Read-only "My Invoices" view for project managers (`Invoices_ListForProjectManager`), scoped to the caller's own managed project(s) — not a relaxed admin gate, since invoices span clients/projects a PM shouldn't fully see.
- `24fd8c7` — `Project.ProjectType` (Development/Support/Contract) and `Project.ProjectManagerUserId`; new `RequireAdminOrProjectManager` authorization check wired into `EntryFlagsFunctions` (previously explicitly deferred pending this entity).

Prior session's commits (`80e86fe` and earlier) are unchanged — see git log.

All committed to `master`. Backend builds clean, 11/11 tests pass (was 6/6 — added RateCard-discount, and 4 `ProjectEstimateService` tests). Angular builds clean (`node node_modules/@angular/cli/bin/ng.js build` — `ng`/`npx tsc` aren't directly runnable in this environment, `node_modules/typescript` has no `bin/` folder for some reason; the Angular CLI's own bundler works fine and was used for every build check this session).

## Scope decisions made this session (worth knowing before touching this code)

- **`EntryTypeId` is optional, not required**, on `CreateTimesheetEntryRequest`/`UpdateTimesheetEntryRequest`, deviating from a literal reading of the FDD ("selecting the entry type" sounds mandatory). Every existing project has zero `EntryType` rows today — making it mandatory would have broken time-logging for every project until an admin backfills entry types everywhere. When supplied, it's validated (belongs to the entry's project, active, and Contract-type gating). Revisit once EntryTypes are actually populated for real projects — HANDOFF item 3 below (Consolidated Staff screen) is unrelated but similar backfill-dependency pattern to watch for.
- **PM invoice visibility is a separate endpoint** (`Invoices_ListForProjectManager`), not a relaxed `Invoices_ListByClient` gate — because `Clients_List` (needed to pick a client in the existing admin Invoicing page) is itself Admin-only, and widening that has broader implications (client PII) outside this feature's scope. A PM instead gets a dedicated read-only "My Invoices" page/route, line-items pre-filtered server-side to their own project(s).
- **Estimated cost/profit hours allocation**: `StaffProject.AllocatedHoursPerWeek` is used as the weighting for splitting `Project.BudgetHours` across assignees (equal split if nobody has it set). This was a genuine ambiguity in the FDD (there's no per-assignment "allotted hours" field) — confirmed with the user rather than assumed.
- **Discounts are percentage-based** (0–100), not a fixed currency amount — also confirmed with the user, FDD didn't specify a unit.
- A RateCard discount is baked into the rate at resolution time (`RateResolver.ResolveAsync`), **not** re-applied at invoice generation — invoicing sums each `TimesheetEntry`'s already-frozen `ResolvedCustomerRate`, so applying it twice would double-discount.
- An invoice-line discount is **wiped if a Draft is regenerated** (`GenerateDraftInvoiceAsync` fully clears/rebuilds `LineItems` on every regenerate of an existing Draft) — a known, accepted limitation, not a bug. Regenerating is a "start over" action and the FDD doesn't require discounts to survive it.

## What's still open (FDD misalignments, not yet started)

In rough priority order:

1. **Consolidated Staff screen** — FDD wants a single Staff screen (replacing today's separate Users/Roles/Rate Cards pages), filterable by admin/active/role/project.
2. **Tenant-wide account browsing** — admin can enable/disable any account within the SVG IT tenancy, not just app-provisioned users.
3. **Notification settings/thresholds** — FDD specifies 50%/75% project consumption thresholds, configurable in settings (currently hardcoded via `Project.BudgetAlertThresholdPercent` only).
4. **Export presets/filters** — CSV export by client/project/PM, all-or-specific users, last-7-days/last-calendar-month/custom-range presets.
5. **Billing/payroll timers** — timer-triggered Functions for monthly recurring billing/roll-forward and out-of-hours payroll aggregation (some of this may already partly exist via `TimesheetReminderFunction`/`NightlyProjectHealthAssessment` — needs checking against the FDD's exact spec). Note: FDD also says "Out of hours work must be approved by project managers or administrators" — the existing `ApprovedPayroll`/`ApprovedByStaffId` fields on `TimesheetEntry` are still Admin-only-implied (not wired to `RequireAdminOrProjectManager` this session) - worth revisiting alongside this item.
6. **Blob Storage for invoice PDFs** — FDD wants generated invoice PDFs (and attachments) in Blob Storage, not the DB. **Needs your input first** on what Azure Storage setup actually exists in dev/prod before this is planned.

## Known loose ends / flags already raised, not yet actioned

- The Roles created earlier (Director, Senior Consultant, Consultant) have **no default RateCard rows** — role-based fallback resolution will fail for any staff/client/project combo not already covered by a person-level override, until someone adds one via the Rate Cards admin page.
- `ProjectsAdminService.listByClient` (frontend) has a pre-existing bug using backslashes instead of forward slashes in its URL template — noted, not fixed, out of scope of everything done so far.
- Newly-added `EntryFlagsFunctions.RaiseManual` UI (entry-flags-page) takes a raw Timesheet Entry Id typed in by hand rather than a picker — a reasonable follow-up would be adding a "Flag" button to the relevant per-entry rows in Approvals/Reports instead.
- No projects have `EntryType` rows yet (brand new this session) — the "Entry Type" picker on the Add Entry page only appears once an admin adds at least one via a project's new "Entry Types" page.
- No projects have `ProjectManagerUserId` set yet — the new PM-only surfaces (My Invoices, EntryFlags scoping, EstimatedCost visibility) will show nothing/403 until an admin nominates a PM on each project's edit page.

## How to resume

Start a new session in this repo and say:

> Read HANDOFF.md and continue the FDD-alignment work from where it left off.
