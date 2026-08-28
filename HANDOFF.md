# TimeSheet — FDD Alignment Handoff (as of 2026-08-28)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done this session (commits, newest first)

- `a41eacb` — Tenant-wide Entra directory search: `GraphAdminUserService.SearchTenantUsersAsync`
  (Graph `$search` on displayName/mail) exposed via `AdminUsers_SearchTenantDirectory`, wired into
  the Staff screen's SSO invite flow so an admin can find a tenant member by name/email instead of
  typing a raw Entra Object Id blind. **Needs a new `User.Read.All` Graph application permission
  consented on the `GraphAdmin` app registration in the real tenant before it returns real
  results** — confirmed with you as a follow-up outside this session; the code path is otherwise
  complete and will surface a clean error until then. "Enable/disable" was scoped, confirmed with
  you, to remain app-level `IsActive` access control only — this never calls Graph to modify the
  person's actual Entra/Microsoft 365 account.
- `bf54538` — Consolidated Staff screen: search/admin/active/role/project filters on the Users
  ("Staff") page; a "assignments across all projects" panel per staff member (new
  `ProjectAssignments_ListByUser`) showing a resolved-rate-and-source preview per assignment via
  `IRateResolver` (previously only ever used as a discard-the-result eligibility check); a fix
  for a pre-existing bug where cost history was fetched but never rendered; a new
  `Projects_ListAll` endpoint for the project filter/picker. Roles and Rate Cards remain
  separate pages — see the correction note below, this item was narrower than HANDOFF originally
  described.
- `1d8f627` — Estimated cost/profit projection per project (`IProjectEstimateService`), computed on read from assigned staff × resolved rate/cost, BudgetHours split weighted by `StaffProject.AllocatedHoursPerWeek` (equal split fallback). Shown on the Project edit page.
- `5fd4f96` — Percentage discounts: `RateCard.DiscountPercent` (baked into `IRateResolver`'s resolved rate, since invoicing never re-resolves) and `InvoiceLineItem.DiscountPercent`/`GrossAmount` (settable only while an invoice is Draft, via `Invoices_ApplyLineItemDiscount`).
- `3a4ebf9` — Per-project `EntryType` (admin-defined list, `IsContractType` gated by the new `Project.ProjectType`), wired onto `TimesheetEntry.EntryTypeId` (optional — see deviation note below).
- `d6d1d1d` — Read-only "My Invoices" view for project managers (`Invoices_ListForProjectManager`), scoped to the caller's own managed project(s) — not a relaxed admin gate, since invoices span clients/projects a PM shouldn't fully see.
- `24fd8c7` — `Project.ProjectType` (Development/Support/Contract) and `Project.ProjectManagerUserId`; new `RequireAdminOrProjectManager` authorization check wired into `EntryFlagsFunctions` (previously explicitly deferred pending this entity).

Prior session's commits (`80e86fe` and earlier) are unchanged — see git log.

All committed to `master`. Backend builds clean, 11/11 tests pass (unchanged this pass — the new
Graph search method has no unit test since it's a thin wrapper over a live external call with no
local fake/emulator equivalent to test against, consistent with `GraphEmailSender`/
`ForcePasswordResetAsync` also being untested). Angular builds clean (`node node_modules/@angular/cli/bin/ng.js build` — `ng`/`npx tsc` aren't directly runnable in this environment, `node_modules/typescript` has no `bin/` folder for some reason; the Angular CLI's own bundler works fine and was used for every build check this session).

Local dev servers were left running this session: API on `http://localhost:7071` (`func start`
from `src/TimeSheet.Api`), Angular on `http://localhost:3000` (`npm start` from
`src/TimeSheet.Web`, watch mode, hot-reloads on save — **but the API host does not hot-reload
C# changes**, it needs a restart after any backend edit). The actual dev SQLite file lives at
`C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\timesheet.db`, not the repo-relative path
`local.settings.json` names (something overrides the connection string on this machine — not
investigated further). It already has demo data seeded from an earlier session: an Admin account
(`mark.llewellyn@svgit.co.uk`, password not `Demo123!` — this machine's own, set previously) plus
three local demo Users at password `Demo123!` each: `sarah.chen@svgit.co.uk`,
`james.obrien@svgit.co.uk`, `priya.patel@svgit.co.uk`.

## Scope decisions made this session (worth knowing before touching this code)

- **`EntryTypeId` is optional, not required**, on `CreateTimesheetEntryRequest`/`UpdateTimesheetEntryRequest`, deviating from a literal reading of the FDD ("selecting the entry type" sounds mandatory). Every existing project has zero `EntryType` rows today — making it mandatory would have broken time-logging for every project until an admin backfills entry types everywhere. When supplied, it's validated (belongs to the entry's project, active, and Contract-type gating). Revisit once EntryTypes are actually populated for real projects — HANDOFF item 3 below (Consolidated Staff screen) is unrelated but similar backfill-dependency pattern to watch for.
- **PM invoice visibility is a separate endpoint** (`Invoices_ListForProjectManager`), not a relaxed `Invoices_ListByClient` gate — because `Clients_List` (needed to pick a client in the existing admin Invoicing page) is itself Admin-only, and widening that has broader implications (client PII) outside this feature's scope. A PM instead gets a dedicated read-only "My Invoices" page/route, line-items pre-filtered server-side to their own project(s).
- **Estimated cost/profit hours allocation**: `StaffProject.AllocatedHoursPerWeek` is used as the weighting for splitting `Project.BudgetHours` across assignees (equal split if nobody has it set). This was a genuine ambiguity in the FDD (there's no per-assignment "allotted hours" field) — confirmed with the user rather than assumed.
- **Discounts are percentage-based** (0–100), not a fixed currency amount — also confirmed with the user, FDD didn't specify a unit.
- A RateCard discount is baked into the rate at resolution time (`RateResolver.ResolveAsync`), **not** re-applied at invoice generation — invoicing sums each `TimesheetEntry`'s already-frozen `ResolvedCustomerRate`, so applying it twice would double-discount.
- An invoice-line discount is **wiped if a Draft is regenerated** (`GenerateDraftInvoiceAsync` fully clears/rebuilds `LineItems` on every regenerate of an existing Draft) — a known, accepted limitation, not a bug. Regenerating is a "start over" action and the FDD doesn't require discounts to survive it.
- **Consolidated Staff screen is narrower than HANDOFF originally described.** The FDD's "Staff"
  section (Users + Staff Costs + a staff member's own project assignments) is a distinct section
  from "Staff Roles and Rate Cards" — Roles and Rate Cards stay as their own admin pages where
  rates/roles get *configured*; the Staff screen only *displays* the resolved rate/tier per
  assignment. An earlier draft of this file said the Staff screen would replace "Users/Roles/Rate
  Cards" — that was wrong, corrected once the FDD text was re-read carefully during planning.
- The Staff screen's nav label and page title say "Staff," but the route/component underneath are
  still `admin/users`/`UsersListPage` — a deliberate minimal-diff choice, confirmed with the user
  rather than doing a full rename.

## What's still open (FDD misalignments, not yet started)

In rough priority order:

1. **Notification settings/thresholds** — FDD specifies 50%/75% project consumption thresholds, configurable in settings (currently hardcoded via `Project.BudgetAlertThresholdPercent` only).
2. **Export presets/filters** — CSV export by client/project/PM, all-or-specific users, last-7-days/last-calendar-month/custom-range presets.
3. **Billing/payroll timers** — timer-triggered Functions for monthly recurring billing/roll-forward and out-of-hours payroll aggregation (some of this may already partly exist via `TimesheetReminderFunction`/`NightlyProjectHealthAssessment` — needs checking against the FDD's exact spec). Note: FDD also says "Out of hours work must be approved by project managers or administrators" — the existing `ApprovedPayroll`/`ApprovedByStaffId` fields on `TimesheetEntry` are still Admin-only-implied (not wired to `RequireAdminOrProjectManager` this session) - worth revisiting alongside this item.
4. **Blob Storage for invoice PDFs** — FDD wants generated invoice PDFs (and attachments) in Blob Storage, not the DB. **Needs your input first** on what Azure Storage setup actually exists in dev/prod before this is planned.

## Known loose ends / flags already raised, not yet actioned

- The Roles created earlier (Director, Senior Consultant, Consultant) have **no default RateCard rows** — role-based fallback resolution will fail for any staff/client/project combo not already covered by a person-level override, until someone adds one via the Rate Cards admin page.
- `ProjectsAdminService.listByClient` (frontend) has a pre-existing bug using backslashes instead of forward slashes in its URL template — noted, not fixed, out of scope of everything done so far.
- Newly-added `EntryFlagsFunctions.RaiseManual` UI (entry-flags-page) takes a raw Timesheet Entry Id typed in by hand rather than a picker — a reasonable follow-up would be adding a "Flag" button to the relevant per-entry rows in Approvals/Reports instead.
- No projects have `EntryType` rows yet (brand new this session) — the "Entry Type" picker on the Add Entry page only appears once an admin adds at least one via a project's new "Entry Types" page.
- No projects have `ProjectManagerUserId` set yet — the new PM-only surfaces (My Invoices, EntryFlags scoping, EstimatedCost visibility) will show nothing/403 until an admin nominates a PM on each project's edit page.
- The local dev API host logs repeated `NightlyProjectHealthAssessment`/`DailyTimesheetReminder` timer-function storage-connection warnings on startup (`AzureWebJobsStorage=UseDevelopmentStorage=true` with no Azurite/storage emulator actually running). Pre-existing, harmless to HTTP endpoints, not investigated or fixed this session.
- Tenant directory search (`AdminUsers_SearchTenantDirectory`) will 500/error until `User.Read.All` is Entra-admin-consented on the `GraphAdmin` app registration — see `GraphClientFactory.cs`'s doc comment for the full list of permissions that registration now needs (`User-PasswordProfile.ReadWrite.All`, `Mail.Send`, `User.Read.All`).
- Noticed in passing, not touched: `AdminUsersFunctions.ResetPassword` only logs password resets via `ILogger`, not the `AuditLog`/`IAuditLogService` this session's earlier work (and last session's `80e86fe`) added — its own doc comment still says "A dedicated AuditLog table would be a natural future enhancement," which is no longer true, it just hasn't been wired up here yet.

## How to resume

Start a new session in this repo and say:

> Read HANDOFF.md and continue the FDD-alignment work from where it left off.
