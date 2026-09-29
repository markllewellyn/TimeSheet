# TimeSheet — FDD Alignment Handoff (as of 2026-09-28)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done in this session, 2026-09-29 (after the demo) — Fixed Fee revenue split per row by hours

The follow-up held back for the demo. Fixed Fee per-user/role/team Profit report rows and the Project Breakdown's
By Staff table used to get revenue 0 (profit = -cost, reading as a loss), papered over with "—" in the UI.

- New `RevenueAllocation.ByHours` (Infrastructure/Services): splits a total by hours, 2dp, rounding remainder to
  the row with most hours, so rows always sum to exactly the total. `ReportingService`'s three per-row Profit
  methods now share `AllocateFixedFeeRevenueAsync` (recognized revenue for the range, split across the rows), and
  `totalBilled` is simply the rows' sum for both payment models. `ProjectBreakdownService` does the same for
  staff lines against `RecognizedRevenueToDate`. By-client report and the T&M path unchanged; non-invoiceable
  Fixed Fee still recognizes 0.
- UI: both "—" workarounds removed (`reports-page`, `project-detail-page`); each keeps a one-line note that Fixed
  Fee rows are a share by hours. Handbook's My Projects (Breakdown) and Reports passages rewritten.
- New `FixedFeeRevenueSplitTests.cs` (5 tests; the two service tests confirmed failing against the old code).
  80/80 backend, 2/2 UI, `ng build` clean, API restarted.
- **Checked against real dev data** with a read-only scratch run of the real services: ERP Migration Phase 2
  (EUR) - summary 17,198.33 = user/role/team row sums = Breakdown staff sum (Sarah 84h 11,900.00, Mark 37.4h
  5,298.33); POS Rollout (USD) - rows = summary (7,800.00 to 8 Sep), Breakdown 8,550.00 (57h), both $150/h =
  45,000 / 300h. Not clicked through in the browser (Chrome not connected).
- Side note from that run: a POS Rollout Profit report whose range includes the GBP expense (9 Sep) needs a
  GBP->USD rate - the real app fetches and caches one online; just be aware it would error offline.

## Done in this session, 2026-09-29 (demo day, before the demo) — Reactivate a client

Found live by the user: a client (Priya's POS Rollout project's client) was missing from the Reports client picker.
Cause: the client was inactive - Reports lists active clients only - and there was **no way to reactivate** a
client at all (`Clients_Deactivate` existed; `Clients_Update` never touches `IsActive`; the list page showed no
button for inactive rows).

- New `Clients_Reactivate` (`POST clients/{id}/reactivate`, Admin-only, mirrors `Clients_Deactivate` exactly -
  neither writes an AuditLog row). **Reactivate** button (with confirm) on inactive rows in Admin → Clients.
- New `tests/TimeSheet.Api.Tests/ClientsFunctionsTests.cs` (3 tests: admin reactivates + client reappears in
  the active list; non-admin refused; unknown id 404). `dotnet test` 73/73, `ng build` clean, API host restarted
  and `Clients_Reactivate` confirmed registered (401 unauthenticated). Handbook's Clients row updated.
- Not live-clicked by me (Chrome not connected) - the user is reactivating the real client themselves.

**Invoice audit logging fixed later the same day - a real, long-standing bug.** `InvoicesFunctions` called
`IAuditLogService.LogAsync` for Void/Delete Draft but had no `IUnitOfWork`, and `LogAsync` only stages the row -
so none were ever saved (dev DB: 11 voided invoices, zero `Invoice.Voided` rows). Now injects `IUnitOfWork` and
saves after every audit row, and also audits `Invoice.DraftGenerated`, `Invoice.Finalized`,
`Invoice.LineDiscountChanged` / `Invoice.ProjectDiscountChanged` (old -> new %). New `InvoicesFunctionsTests.cs`
(2 tests, confirmed failing with the save removed); 75/75 pass; API restarted. Past invoice actions can't be
recovered. **Worth checking other Functions classes for the same stage-without-save trap** - the others looked
fine (their action counts appear in the AuditLog table), but it's easy to miss.

**"Payroll Periods" renamed to "Out-of-Hours Payroll"** on screen (nav link, page title, empty/error messages)
and in the handbook, at the user's request - the old name read as all payroll. Display text only: the
`/admin/payroll-periods` route, API, `PayrollPeriod` entity and code names are unchanged (the FDD's own data model
still calls it PayrollPeriod).

Also added a "Costs, rates and roles" subsection to the handbook's Admin chapter. Found while writing it: the
person + project rate tier (resolver step 1) has **no UI** - only per-client person overrides can be created.

**Demo dummy data added later the same day, at the user's request (direct DB write, approved; backup of the data
taken first to `OneDrive - SVG IT\Projects\Claude\Training\TimeSheet-DataBackups\2026-09-29\`)** - so Payroll
Periods has June and July rows, not just August:
- 6 out-of-hours entries, posting batch **`DEMO-DUMMY-JUN-JUL`**, description "Out-of-hours support call (dummy
  data)", approved (by Mark) but **not** sent: James O'Brien on WMS Support Retainer (Jun 10: 3h, Jun 20: 2h,
  Jul 8: 4h, Jul 18: 1.5h), Mark on Warehouse Ops Optimisation (Jun 13: 2h, Jul 22: 3h).
- Pricing them needed backdated copies (same rates, `EffectiveFrom` 2026-06-01) of James's and Mark's StaffCost
  rows and their Northwind person+client RateCards - all dev data previously started 2026-08-10.
- PayrollPeriod rows for June (7h, 1,245.00) and July (8.5h, 1,537.50) built with the real
  `PayrollAggregationService` from a scratch tool (Run Now can only build the previous month).
- To remove: delete entries with that posting batch, the two June/July PayrollPeriods, and the four
  `EffectiveFrom = 2026-06-01` StaffCost/RateCard rows - or restore the backup.

**Post-demo backlog raised today, not started (user's call):**
- ~~**Fixed Fee per-row revenue attribution**~~ - **done after the demo, same day** (see the entry at the top).
- **Reports can't report on an inactive client** - `reports-page.ts` calls `clientsService.list()` (active only).
  Possible fix: a "Show inactive clients" toggle.
- **Time can still be logged against an inactive client** if the person's project assignment is active. Decide
  whether deactivating a client should block that.
- **Fixed Fee recognized revenue isn't capped** at `FixedFeeAmount` once hours pass `BudgetHours`
  (`RevenueRecognitionService`).
- **Expenses have no approval step** - confirmed 2026-09-29 against the full FDD (text, reviewer comments and both
  diagrams): none is required - the only approval the FDD asks for is out-of-hours work. A new requirement if wanted.
- **Audit rate and cost changes** - the FDD's architecture diagram asks for a "row-level audit trail on rates,
  entries and invoices". Entries are audited and invoices are now too, but creating Rate Cards
  (`RateCardsFunctions`, which also backs client Rate Overrides) and Staff Cost History rows (`StaffCostsFunctions`) writes no AuditLog row. Add one per
  create (who, scope, rate/cost, effective date), saving via `IUnitOfWork` - see today's `InvoicesFunctions` fix for
  the stage-without-save trap. Client Deactivate/Reactivate aren't audited either; worth doing at the same time.
- **No screen for a person + project rate** (resolver step 1) - the API supports it, the UI only creates
  per-client person overrides. Add if per-project exceptions for individuals are wanted.

## Done in this session, 2026-09-28 — pre-demo check: tests, handbook audit, one real locking bug

User has a demo on 2026-09-29. Dev stack restarted fresh (Azurite, `func start`, `npm start` - same commands as
before). Chrome was **not connected** this session, so there was no live browser walk - see "Still to do" below.

- **Tests**: `dotnet test TimeSheet.slnx` 70/70 (68 Infrastructure + 2 Api; Domain.Tests has none). `ng build`
  clean. `ng test --watch=false` was **failing 2/2 and always had been** - `app.spec.ts` was the untouched CLI
  scaffold (expected a "Hello, TimeSheet.Web" `<h1>`; jsdom has no `matchMedia` for `ThemeService`). Rewritten
  as a real shell smoke test (router/HttpClient providers, `matchMedia` stub, asserts the signed-out "Sign in"
  link) - 2/2 pass.
- **API smoke**: every GET route from the `func start` listing probed unauthenticated - 57 × 401, 2 × 302 (Entra
  login/callback), no 404/500. Startup log's only errors are the known `DailyTimesheetReminder` Graph
  `noreply@svgit.co.uk` failure and harmless Entra-scheme "kid is missing" noise when a local JWT is tried
  against the Entra scheme first (every function execution in the log Succeeded).
- **Real bug fixed - Log Time grid locking**: the server refuses Edit/Delete/Duplicate once an entry is
  `ApprovedPayroll` (`TimesheetEntriesFunctions` Update/Delete/Duplicate), but `entry-actions-cell.ts` only hid
  the buttons once `SentToPayroll`/invoiced - an approved-but-unsent entry showed buttons that just errored.
  Now also locks on `approvedPayroll`, with a new "Locked (approved)" label.
- **Stale hint fixed**: Project Edit page said rates are "set per staff member on the client's Staff Costs
  section, not per project" - predates Rate Cards; `RateResolver` has five tiers incl. role+project and
  person+project. Reworded to point at Rate Cards (now linked).
- **Handbook audit** (full route-by-route pass vs templates): nav labels all matched, but fixed four factual
  errors (Download PDF on a Draft - it's Finalized/Voided only; Calendar day-click opens an in-page panel, not a
  jump to Log Time; Reports' Client is required, not optional, and preset names; Approvals said *sending* locks
  an entry - it's approving) plus the Resolved Rate glossary order (now the real five tiers), and filled gaps:
  not-provisioned banner, Sign out / session-expired redirect, Log Time summary cards, the Admin "approved and
  sent to payroll immediately" checkbox, Delete Draft / Void Invoice / Reason, View Breakdown, Edit / Rates,
  Fixed Project Cost, Staff Cost History + Find in tenant directory, Settings threshold names, CSV header names,
  Billable column, My Invoices/My Projects nav visibility, Admin menu location.
- **Live click-through done later the same session** (Chrome connected, signed in as Admin by the user): all
  23 main routes load with no console errors or error banners; "Locked (approved)" confirmed on entry 24 (and
  "Locked (sent to payroll)" still on entry 22); the Project Edit hint renders with its Rate Cards link; a real
  Profit-by-Team report run for ERP Migration Phase 2 returned correct figures.
- **Two more fixes from that walk**: (1) the header nav links (Log Time, Calendar, ...) showed while signed out -
  now behind `currentUser.isSignedIn()`, the same check the right-hand header side already used; `app.spec.ts`
  asserts no nav links when signed out. (2) Project Detail Breakdown's By Staff table showed Revenue 0.00 and a
  negative Profit per person on a Fixed Fee project (reads as a loss) - now "—" whenever
  `recognizedRevenueToDate` is set, matching what the handbook already claimed.
- **Login-page "Create the first Admin account" link removed** (user chose this over a backend fix): it always showed, even on a
  populated DB (the bootstrap endpoint just 403s). Fixing it properly needs an anonymous "is bootstrap
  available" endpoint whitelisted in `CurrentUserMiddleware`; that edit was **blocked by this session's safety
  classifier** (auth-middleware change). First-time setup on an empty DB now goes to `/bootstrap-local` directly by URL.
- **Fixed later the same session**: Profit reports' per-user/role/team rows showed Billed 0 / negative Profit on
  Fixed Fee projects (revenue is recognized once, in the summary). `reports-page` now shows "—" for Billed/Profit
  on those rows plus a one-line note, only for an invoiceable Fixed Fee project; backend unchanged.
- ~~**Possible follow-up, deliberately not done before the 2026-09-29 demo (user's call)**~~ **Done 2026-09-29 after the demo** (see the top entry): attribute Fixed Fee revenue per
  row by hours (row hours / BudgetHours x FixedFeeAmount - recognition is already hours-linear, so rows would sum to
  the summary bar expenses/rounding). Touches ReportingService's 3 per-row methods + ProjectBreakdownService, and
  would replace both "—" front-end fixes above. ~25 min incl. a sum-to-summary test.

## Done last in this session, 2026-09-11 — a UI polish sweep, honestly reported: found the app already in good shape

With the FDD list and known loose ends closed, the user asked for "a bit of UI polish and testing." Rather than
inventing busywork, re-ran the same class of checks the 2026-09-08 polish pass used (native `confirm()`/
`alert()`, hardcoded Tailwind colors instead of theme tokens, missing confirm-before-destructive-action,
inconsistent empty-state treatment) across everything built since then (Team, per-role rollup, Hours Remaining,
Void/Delete invoice, Project Detail Breakdown, My Projects) - all clean, no regressions found. A live visual
walk of Reports, Invoicing, Export, My Projects, and the Breakdown page also turned up nothing obviously broken.

- **One real thing found and fixed, though its actual impact couldn't be confirmed**: while investigating why
  the Project Detail Breakdown's pie charts render smaller than their card (a purely cosmetic observation, not
  a functional bug), a `javascript_tool` measurement of the actual `<canvas>` elements returned a wildly
  implausible `clientWidth`/`clientHeight` (~2453px, bigger than the whole page) that didn't reconcile with the
  correct-looking rendered screenshot at all, and further investigation (checking for CSS transforms/zoom -
  none found) couldn't explain the discrepancy. **`chart-canvas.ts`'s wrapper div was missing `position:
  relative`**, which Chart.js's own docs require on a canvas's immediate parent for its responsive-resize
  logic to measure the correct ancestor - a real, independently-justified gap regardless of the mystery
  reading, so fixed it. Confirmed via a fresh measurement afterward that this did **not** change the odd
  number (so it wasn't the actual explanation for that specific reading) and confirmed via screenshot the
  visual rendering is pixel-identical before/after - a safe, correct-per-the-docs addition, not a fix for a
  confirmed live bug. Flagging the unexplained measurement here rather than quietly dropping it, in case it
  points at something real that a future session with a differently-behaving browser environment can actually
  pin down.
- Angular build clean. Frontend-only change (`chart-canvas.ts`), no backend/DB change, no API host restart
  needed.
- **Nothing else was changed** - the honest result of this pass is that the app doesn't have an obvious backlog
  of small polish items left; the last two sessions' work (loading-state consistency, confirm-dialog coverage,
  color tokens, empty-state structure) already covers the codebase pretty thoroughly. Told the user this
  directly rather than manufacturing changes to look busy.

## Done later still in this session, 2026-09-11 — actually found the Entry Flags search bug, plus closed two other "Known loose ends" verification gaps

With the FDD-numbered backlog closed, picked up two items from "Known loose ends" at the user's request: (1)
try to reproduce the long-standing, never-confirmed "my own entries go missing from an Entry Flags search"
report, and (2) live-verify the two items previously only verified by code review (Clients' BillingPeriod
fields, the PM-scoped batch-rejection path).

**(1) Found a real, previously-undiscovered root cause - not just re-confirmed the existing 25-result-cap
mitigation.** `SearchForFlaggingAsync`'s `OrderByDescending(e => e.Date)` has no secondary sort key. Queried the
real dev DB directly (a throwaway `Microsoft.Data.Sqlite` console script, cleaned up after) for three broad
searches ("test", "Mark", "Sarah") ordered exactly like the real query, and in **all three**, the take-25 cutoff
landed squarely inside a run of same-date entries - meaning which entry survived into the results and which
silently vanished was decided by SQLite's own implementation-defined tie order (in practice, ascending
insertion order), not any real recency signal. A very plausible match for the original report.

- **Fix**: `.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)` - `Id` (autoincrement) as a tiebreak
  makes the ordering deterministic and biases same-date ties toward the most-recently-created entry, matching
  what "most recent first" actually implies.
- **New `TimesheetEntryRepositoryTests.cs`** (first test file for this repository) - seeds 30 same-date entries
  matching one search term (more than the take=25 cap), asserts the result is identical across two calls
  (deterministic) and is exactly the 25 highest-Id rows. **Confirmed this genuinely fails without the fix** -
  reverting to `OrderByDescending(Date)` alone made the in-memory SQLite provider return the 25 **oldest** rows
  (Ids 1-25 ascending), the opposite of "most recent," which is an even stronger confirmation than expected.
- Backend build clean, 68/68 Infrastructure tests pass (1 new). **Live-verified against the exact real data that
  proved the bug**: searched "test" in the raise-a-flag picker as Admin - entry #117 ("second verification
  entry", Sarah Chen, Aug 21), confirmed via the same DB script to be excluded under the old ordering, now
  correctly appears in the results. API host restarted (repository method body change) - confirmed clean
  startup.
- **This is now believed fixed, not just mitigated** - upgrading the "Known loose ends" entry below accordingly,
  though the original report was never reproduced under controlled conditions, so keep an eye out.

**(2a) Clients' BillingPeriod/CurrentPeriodStart/CurrentPeriodEnd fields, live-verified directly against
`Clients_Create`/`Clients_Update`** (previously only verified indirectly via the roll-forward timer). Used
Antigua (client 5) as a real, reversible test: switched Billing Period to Monthly via the UI, confirmed via
`read_network_requests` a real `PUT /api/clients/5` returned 200, then reloaded the Edit page and confirmed
`CurrentPeriodStart`/`CurrentPeriodEnd` had round-tripped correctly (`2026-09-01`/`2026-09-30`, the current
calendar month - `ResolveBillingPeriod`'s own fallback for switching to Monthly with no explicit dates given).
Reverted back to One-off afterward (a stray first attempt didn't register - the click-flakiness this form has
been noted for before - confirmed via `read_network_requests` that a second attempt's `PUT` actually fired
before trusting the revert) - confirmed Current Period Start/End correctly disappeared again.

**(2b) The PM-scoped batch-rejection path (`CheckOutOfScopeAsync`), live-verified as far as safely possible,
plus a new proper test closing the rest of the gap.** Reset Sarah Chen's password (Admin action, temp password
generated) to sign in as a real non-admin PM rather than test as Admin (whose `IsAdmin` bypasses the whole
check) - confirmed her Approval Queue is correctly scoped to only her own managed project (25 entries, all ERP
Migration Phase 2), reconfirming the underlying `GetManagedByUserAsync`-based scoping. Attempted to go further
and forge an actual mixed-project batch request (the real UI never shows an out-of-scope entry to select, so
only a hand-crafted request could exercise the true rejection branch) via a `fetch()` call using the browser's
own stored session token - **blocked by this session's own safety classifier** (reading/using the local auth
token is treated as sensitive regardless of purpose). Did not attempt to work around this; pivoted to the
legitimate alternative instead:
- **New `tests/TimeSheet.Api.Tests/TimesheetApprovalFunctionsTests.cs`** - the first test for this codebase's
  Functions layer, which had zero test coverage before now. Constructs a real `TimesheetApprovalFunctions`
  against real in-memory-SQLite-backed repositories, a stub `ICurrentUserAccessor` (a genuine non-admin PM), and
  a hand-built `HttpRequest` (`DefaultHttpContext` + a JSON body stream - no auth/network involved at all).
  2 tests: a PM's batch mixing one managed-project entry with one entirely unrelated project's entry is rejected
  with a real `BadRequestObjectResult`, **and** the in-scope entry is confirmed NOT partially approved (the
  class's own documented no-partial-success intent); a control test confirms an all-in-scope batch still
  succeeds normally. **Confirmed the rejection test genuinely fails without the fix** (temporarily short-
  circuited `CheckOutOfScopeAsync` to always return null/no-rejection, re-ran, saw the mixed batch wrongly
  succeed, restored it).
- Backend build clean, 2/2 new Api.Tests pass (70/70 total across both test projects). This closes the loose
  end for real, not just re-stating "verified by code review."
- **New demo-data side effect**: Sarah Chen's local-account password was reset during this verification (a
  one-time temporary password was generated and used to sign in as her, then discarded) - same as the
  established 2026-09-08 precedent for testing as a real PM. No lasting behavior change, just a changed
  password on a fictional dev-seeded account.

## Done later still in this session, 2026-09-11 — the "Team" concept, item #2 and the last item on the FDD audit's numbered backlog

FDD's only mention of "team" anywhere in the whole document, confirmed by a fresh extraction of the docx's raw
text (no cached copy existed): "...totals for projects and clients on a team, role and user basis." No
definition anywhere, no reviewer comment either - genuinely undefined in the source, which is exactly why this
was flagged as a business-model decision rather than guessed at. Quoted this to the user directly, offered a
few shapes it could take (a staff grouping like Role, a project's assigned staff, a department/practice area, or
"not sure, discuss"); their answer: **"just make it a staff grouping like Roles, don't overthink it."**

- **A dedicated research pass mapped the entire Role feature first** (entity, EF config, migration, repository,
  Functions CRUD, Contracts DTOs, User's FK, the admin CRUD page, nav/routing, and the "by Role" Reports
  pipeline end to end - `IReportingRepository`'s LEFT JOIN, `IReportingService`'s 3 "OnProjectByRole" methods,
  `ReportsFunctions`'s gating, the frontend's fully-generic report rendering) before writing a single line, so
  Team could mirror it point-for-point rather than half-copy it.
- **New `Team` entity** (`TimeSheet.Domain/Entities/Team.cs`) - Id/Name/IsActive/CreatedUtc, a literal copy of
  `Role.cs`'s shape, explicitly documented as having **no rate/billing significance of any kind** (unlike Role,
  which `RateCard` resolves against) - Team exists purely as a second, independent reporting/filtering axis.
  New `TeamConfiguration` (unique `Name` index, same as `RoleConfiguration`), new `ITeamRepository`/
  `TeamRepository` (identical shape to `IRoleRepository`/`RoleRepository`, ordered by `Name` - no SQLite
  `ORDER BY DateTimeOffset` trap since nothing orders by `CreatedUtc`), new `TeamDtos.cs`, new `TeamsFunctions`
  (`Teams_List`/`Create`/`Update`, Admin-only, no audit logging - matching `RolesFunctions`'s own precedent
  exactly, including its narrower-than-full-CRUD surface: create + list + soft-deactivate/reactivate, no rename
  UI despite the DTO supporting one).
- **New migration `AddTeam`** - confirmed by the Role-feature research to need none of the original
  `AddRoleAndRateCard` migration's raw-SQL table-rebuild complexity (that was only needed because it *also*
  reshaped `StaffCosts`/`StaffProjects`/dropped legacy columns in the same migration) - a plain EF-auto-diffed
  `CreateTable "Teams"` + `AddColumn Staff.TeamId` + FK + index, generated via `dotnet ef migrations add`
  without incident.
- **`User.TeamId`/`User.Team`** (nullable FK + navigation, mirroring `JobRoleId`/`JobRole` exactly) -
  `UserConfiguration` gained the matching `HasOne(u => u.Team)...OnDelete(Restrict)`; `UserRepository.GetByIdAsync`/
  `GetAllAsync` both gained `.Include(u => u.Team)` alongside the existing `.Include(u => u.JobRole)`, without
  which `ToDto`'s `u.Team?.Name` would have silently stayed null after a save. `UserDto`/`InviteUserRequest`/
  `UpdateUserRequest` all gained `TeamId`(`/TeamName`); `UsersFunctions` validates a given `TeamId` exists
  (404 otherwise) exactly like it already does for `JobRoleId`.
- **The FDD's actual "team... basis" reporting ask, not just an inert entity**: `IReportingRepository`'s
  `TimeEntryAggregateRow` gained `TeamId`/`TeamName` via a second LEFT JOIN (`db.Teams`) right alongside the
  existing Role join in `ReportingRepository.GetTimeEntryAggregatesAsync` - same "Unassigned" sentinel pattern
  when `TeamId` is null. New `TimeByTeamLine`/`CostByTeamLine`/`ProfitByTeamLine` records and 3 new
  `IReportingService` methods (`GetTimeOnProjectByTeamReportAsync`/`GetCostOnProjectByTeamReportAsync`/
  `GetProfitOnProjectByTeamReportAsync`), each a line-for-line mirror of its by-Role counterpart - including the
  same Fixed-Fee-revenue-recognition caveat that keeps by-Role/by-Team reports Project-scoped only, never
  Client-scoped (a client can span Fixed Fee and T&M projects whose recognized revenue can't honestly be split
  by role *or* team - same reasoning, now explicitly extended to Team in `IReportingService`'s own doc comment
  rather than only covering Role). 3 new `ReportsFunctions` endpoints, same Admin-only gating on Cost/Profit as
  their by-Role siblings.
- **No Angular rendering changes needed for the new reports** - confirmed by the Role-feature research before
  writing frontend code: `reports-page.ts`/`.html` render summary tiles and breakdown columns fully generically
  (`Object.keys()` + `keyvalue` pipe) - the 3 new report types only needed adding to `ReportType`'s union,
  `PROJECT_SCOPED`'s array, and 3 new `<option>`s in the dropdown; the results table needed zero changes.
- **New `teams-page`** (`/admin/teams`, Admin-only), new `teams.service.ts` - both a direct copy of
  `roles-page`/`roles.service.ts`, new "Teams" link in the Admin nav dropdown (after Staff, alphabetically
  last). **`users-list-page`** (Staff screen) gained a Team column mirroring Job Role exactly: an invite-form
  select, a list-filter dropdown, a per-row inline reassignment select (`updateTeam()`, copying
  `updateJobRole()`), and every existing `UpdateUserRequest`-building call site (`toggleActive`/`toggleRole`/
  `saveEditPayroll`) threaded `teamId: user.teamId` through unchanged, since none of those actions intend to
  touch a user's team - a genuinely new 9th column meant the expanded "Manage" panel's `colspan` also needed
  bumping from 8 to 9, caught by checking for `colspan` usages before considering the HTML done, not after.
- Backend build clean. 67/67 tests pass (1 new, mirroring the existing by-Role "Unassigned bucket" test exactly
  for Team - two users on the same project, one with a Team, one without, asserting both real-team and
  "Unassigned" lines come back correctly grouped and summed - confirmed this test genuinely fails without the
  fix by temporarily hardcoding the repository's `TeamName` fallback to always `"Unassigned"` and re-running
  before restoring it, same rigor as every other fix this session). Angular build clean. API host restarted
  (new `TeamsFunctions` class + `UsersFunctions`/`ReportingService` constructor changes) - confirmed clean
  startup, migration applied (Teams page loaded with an empty list, not an error).
- **Live-verified end-to-end in the browser with real data, not just the happy path**: created two real teams
  ("Delivery Pod A", "North Region") through the app itself, assigned Mark Llewellyn to one and Sarah Chen to
  the other via the Staff screen's new inline picker (confirmed the assignment persisted after a refresh). Ran
  "Time on Project (by Team)" for Acme's ERP Migration Phase 2 (Last Year range) - got back real per-team
  hours/entry-counts (North Region 84h/17 entries, Delivery Pod A 19.4h/16 entries) alongside the pre-existing
  Budget Hours/Hours Remaining summary tiles, unaffected by the new breakdown. Ran "Profit to Business on
  Project (by Team)" on the same Fixed-Fee project - confirmed each team line correctly showed `Billed: 0` with
  the real recognized revenue appearing once at the summary level only, proving the Fixed-Fee-attribution
  mirroring actually works, not just compiles.
- **New demo-data artifacts**: two real Teams ("Delivery Pod A", "North Region") and two real staff-to-team
  assignments (Mark Llewellyn, Sarah Chen) created during live verification - left in place as genuine, useful
  demo data rather than cleaned up, since they're exactly the kind of real setup this feature needs to be usable
  going forward, not test noise.
- **This closes the FDD re-audit's numbered backlog entirely** - all 4 items from the 2026-09-10 re-audit are
  now either fixed (1, 3, 4) or this one (2), with the two remaining "business decisions surfaced, not bugs"
  (BudgetHours reset, OOH approval timing) deliberately deferred per the user's own call, see the entry below.

## Done later still in this session, 2026-09-11 — "Hours remaining" surfaced in Reports and CSV Export, item #4 from the last FDD re-audit

Picked up from the same open-items list as the per-role rollup above, at the user's own choice from a menu of
the remaining non-business-decision gaps. FDD places "hours remaining" inside the Reports/Export feature; it
only existed via the separate Budget & Cost Status feature (`IProjectStatusService`), never in Reports or the
CSV export. Researched first via a dedicated Explore agent mapping the whole Reports/Export pipeline and
`IProjectStatusService`'s exact shape before writing any code.

- **A real design decision made and documented, not guessed silently**: `Project.BudgetHours` is an all-time,
  non-period-resetting figure everywhere else it's used (see item #5 below, still an open ambiguity) - so the
  new `BudgetHours`/`HoursRemaining` fields are **all-time figures, deliberately not scoped to the report's own
  date Range**, exactly matching `IProjectStatusService`'s own existing convention. This means, e.g., a "Last 7
  Days" report can show `TotalHours: 5` (in-range) alongside `HoursRemaining` computed against the project's
  real all-time actual hours (which may be much larger) - intentional, not a bug, and heavily doc-commented in
  `IReportingService.cs` so a future reader doesn't "fix" the apparent mismatch.
- **A second real design decision**: `GetTimeOnClientReportAsync`'s own client-wide `TimeSummary` deliberately
  leaves `BudgetHours`/`HoursRemaining` **null**, not a summed aggregate - a client's projects can have
  different budgets or none at all, and there's no single honest "hours remaining" number across them (same
  reasoning precedent as the existing Cost/Profit-by-Role Client-scope exclusion documented in
  `IReportingService`'s own doc comment). The per-project truth instead lives on each `TimeByProjectLine` in the
  breakdown, where it belongs.
- **`IReportingService.cs`**: `TimeSummary` and `TimeByProjectLine` both gained optional trailing
  `BudgetHours`/`HoursRemaining` fields (nullable, default `null`) - optional/trailing meant no existing
  positional-record call site needed updating except the ones deliberately populating them.
- **`ReportingService.cs`**: new `IProjectStatusService` constructor dependency (already-registered Scoped
  service, no DI cycle). `GetTimeOnProjectReportAsync`/`GetTimeOnProjectByRoleReportAsync` fetch the project's
  `ProjectStatus` once and thread it through `SummarizeTime`. `GetTimeOnClientReportAsync` bulk-fetches every
  one of the client's projects (`IProjectRepository.GetByClientIdAsync`, `includeInactive: true` - same
  precedent as `InvoiceGenerationService`) and calls `IProjectStatusService.GetStatusesAsync` once for all of
  them (not per project group - avoids an N+1), then looks each project's status up per breakdown line.
- **`AdminExportFunctions.cs`** (CSV export) - **first design rejected by the user, fixed the same session**:
  the first version added two trailing columns, "Project Budget Hours"/"Project Hours Remaining", stamped on
  every row for that entry's project (the same "repeat static per-project context on every row" convention the
  existing Client/Project name columns already use). Live-verified, then the user reported it back immediately:
  "The export looks off, looks to be repeating data in there?" - correctly reading an identical value repeated
  across dozens of rows (for a single-project export especially) as looking like duplicated data, even though no
  entries were actually duplicated. Asked the user directly rather than re-guessing; they chose a **trailing
  summary section** instead - the per-entry rows now stay exactly as they were before this session touched them
  (no new columns at all), with a blank line then one small `Project,Project Budget Hours,Project Hours
  Remaining` table underneath, one row per **distinct project actually exported** (sorted by name), omitted
  entirely when nothing was exported. Distinct projects are bulk-status-fetched once
  (`IProjectStatusService.GetStatusesAsync`), same N+1-avoidance as the Reports change.
- **No Angular changes needed for Reports** - confirmed by reading `reports-page.ts`/`.html` first: the page
  already renders whatever fields a report's JSON happens to contain, fully generically (`Object.keys()` +
  `keyvalue` pipe, camelCase-to-Title-Case label conversion) - the two new fields appear as a new summary tile
  and a new breakdown column automatically. No dedicated Export frontend change needed either (backend-only CSV
  shape change, download is a raw blob).
- Backend build clean. 66/66 tests pass (3 new, all in `ReportingServiceTests.cs`: `GetTimeOnProjectReportAsync`
  with a project's `BudgetHours` set proves `HoursRemaining` reflects true **all-time** actuals - deliberately
  seeds one entry inside the report's date range and one well outside it, asserting `HoursRemaining` accounts
  for both while the report's own `TotalHours` only reflects the in-range one; the same report with no
  `BudgetHours` set asserts both new fields are null, not a misleading zero; `GetTimeOnClientReportAsync` with
  one budgeted and one unbudgeted project proves each `TimeByProjectLine` carries its own correct figures while
  the client-level `Summary` stays null throughout - **confirmed the first two tests genuinely fail without the
  fix** by temporarily hardcoding the new `HoursRemaining` helper to always return `null` and re-running before
  restoring it, same rigor as every other fix this session). No dedicated test added for `AdminExportFunctions`
  - matches this codebase's already-established bar of no test coverage for that thin Functions-layer class.
  Existing 5 `ReportingServiceTests` call sites updated for the new constructor dependency, no assertions
  changed. Angular build unaffected (no frontend files touched). API host restarted (new constructor
  dependencies on two classes) - confirmed clean startup.
- **Live-verified in the browser against real Acme data, both surfaces**: ran a "Time on Project" report
  (ERP Migration Phase 2, Last Year) - summary tiles showed real `Budget Hours: 600` / `Hours Remaining: 496.6`
  alongside the pre-existing tiles, with no template changes needed. Ran "Time on Client" for the same client -
  the client-level summary correctly showed blank Budget Hours/Hours Remaining tiles, while the per-project
  breakdown table showed each project's own real figures, including a genuine **negative** Hours Remaining
  (`-1`) on the small over-budget "Notif Threshold Test" project - real proof the "can go negative" design
  works, not just a happy-path number. Downloaded the real CSV export twice, with the user's explicit permission
  each time for the file download: first filtered to Acme / ERP Migration Phase 2 (2026 date range) - confirmed
  the *reported* problem for real, every row repeating `600.0`/`496.6`; after the fix, re-downloaded with no
  filters at all (every client/project) - confirmed the per-entry rows were back to their original, unmodified
  shape, followed by exactly one trailing summary row per distinct project actually in the export (8 projects,
  including two with no Budget Hours set showing correctly blank, and the same over-budget "Notif Threshold
  Test" project again showing a negative `-1.0` Hours Remaining).
- **New demo-data artifacts**: three real CSV files (`timesheet-export-2026-09-11.csv`,
  ` (1).csv`, ` (2).csv`) saved to the user's own Downloads folder across the two rounds of live-verification
  download above - their own file space, left as-is rather than deleted on their behalf.

## Done in this session, 2026-09-11 — per-role rollup on the Estimated Cost/Profit panel, item #3 from the last FDD re-audit

Picked up from the 2026-09-10 re-audit's open-items list (this file's "What's still open" section) at the
user's own choice, offered among the audit's non-business-decision gaps. FDD: the Estimated Cost/Profit
figures are "calculated per user *and per role*" - the per-user table already existed, but there was no
role-level rollup anywhere.

- **No backend change needed at all** - `ProjectEstimateLine`/`ProjectEstimateLineDto` already carried
  `RoleId`/`RoleName` per line (`ProjectEstimateService.EstimateAsync` already resolves each assignee's role),
  so this was a pure frontend aggregation of data already being returned.
- **New `core/utils/estimate-role-grouping.ts`** - `groupEstimateLinesByRole()`, mirroring the existing
  `groupInvoiceLineItemsByProject()`'s exact shape (a `Map`-keyed sum, sorted by name). A line with a
  rate-resolution `warning` already carries `estimatedCost`/`Revenue`/`Profit` of `0` (set that way in
  `ProjectEstimateService`), so it sums in safely with no special-casing; an unrolled line (`roleId === null`)
  groups under "Unspecified" rather than being dropped, so the rollup's totals always match the per-user
  table's totals exactly.
- **`project-edit-page`**: new computed `estimateByRole` signal, new "By Role" table (Role/Est. Hours/Cost/
  Revenue/Profit) rendered above the existing "By User" table inside the same "Estimated Cost/Profit" card,
  both now under their own subheadings.
- Backend build clean (no change, confirmed anyway since `TimeSheet.slnx` was touched by nothing here).
  Angular build clean. No API host restart needed (frontend-only).
- **Live-verified in the browser** (as Mark Llewellyn, Admin) on two real projects with Budget Hours set -
  "ERP Migration Phase 2" (Director + Senior Consultant) and another Development project (Director +
  Consultant) - the new "By Role" table's rows summed exactly to the card's overall Estimated Cost/Revenue/
  Profit figures in both cases. Also confirmed the empty-budget state (a project with no Budget Hours set)
  still renders its existing "Set Budget Hours above to see an estimate." message unaffected. Both real
  projects checked happened to have exactly one person per role, so this didn't exercise multi-person-per-role
  summation against real data specifically - not worth fabricating demo data just to prove it, since the
  grouping itself is a straightforward `Map`-based sum copying an already-proven pattern.

## Done later in this session, 2026-09-11 — caught the end-user handbook badly out of date, brought it current

The user asked directly, after the per-role rollup fix above, whether `Resources/TimeSheet-Handbook.html` (the
end-user guide, first written 2026-09-04) still reflected the app. Checked before assuming: it had zero
mentions of anything from the entire 2026-09-10 session or the per-role rollup just above it - My Projects,
Project Detail Breakdown, Budget & Cost Status/On Track badges, the Estimated Cost/Profit panel, Delete
Draft/Void invoice, or the per-entry invoice line detail that replaced the old one-line-per-project rollup. It
had kept pace with smaller features along the way (attachments, Save & Attach, search fixes) but not with any
of the larger session's work. **The user's own words: "we should be doing this as we go."** - saved as a
standing practice in this session's memory (`feedback_handbook_maintenance`), so future feature work updates
the handbook in the same pass rather than letting a backlog accumulate again.

- **New §9 "My Projects" chapter** (PM pill) - the On Track badge (Hours %/Cost % vs Budget Hours/Fixed Fee
  Amount, green/amber/red at 75%/100%, "No budget set" otherwise) and the Breakdown drill-down (pie: by staff,
  pie: by entry type, bar: by month; a Fixed Fee project's per-person revenue/profit deliberately left blank in
  favour of one "Recognized Revenue to date" figure, matching Reports' own existing Fixed Fee logic). Everything
  after it renumbered (§10 Payroll Periods … §14 Glossary).
- **§11 Invoicing rewritten**: step 1 now describes real per-entry line items (staff/date/description/hours/
  rate) grouped by project instead of the old one-line-per-project description, mentions already-invoiced
  exclusion and the overlapping-Draft block; step 2 adds the bulk "Discount whole project" control alongside
  the existing per-line discount; step 3 clarifies Fixed Fee locking; step 4 covers Voided PDFs. New "Made a
  mistake?" note covering Delete Draft vs Void (kept record/number/PDF, unlocks entries) - the two big features
  the old text never mentioned at all.
- **§13 Admin → Projects registry row** extended to mention the On Track badge/Breakdown link on the Admin
  project lists and the Budget & Cost Status + Estimated Cost/Profit (by role and by user) panels on a
  project's own Edit page, cross-linked to the new My Projects chapter rather than duplicating its explanation.
- **Glossary**: two new entries, "Voided (invoice)" and "On Track badge", matching the glossary's existing
  status-vocabulary convention.
- No app code touched - documentation only. **Visually verified in the browser**: served the `Resources/`
  folder locally (`npx http-server`, port 8123 - `file://` URLs aren't reachable by the browser automation tool)
  and screenshotted the new My Projects, Invoicing, Admin, and Glossary sections in both the jump-nav and
  content areas - correct dark-mode styling, correct anchor links, correct chapter numbering throughout, no
  layout breakage. Preview server stopped and its log file removed afterward, no artifact left behind.

## Done still later again in this session, 2026-09-10 — closed the last double-billing hole: a stale sibling Draft for an overlapping period is now blocked at generation time

Immediate follow-up to the Fixed Fee fix above, the user's own suggested fix for a gap flagged in that entry's
own "Not fixed" note: `FinalizeInvoiceAsync` never re-validates a Draft's *own already-stored* line items
against current reality. If Draft A and Draft B exist for the same client with overlapping periods (both
legitimately show the same entries/expenses/Fixed Fee, since nothing is locked until Finalize), finalizing A
correctly excludes what it billed from any *new* draft generation - but Draft B itself is never refreshed. Its
line items were computed before A existed and stay stale. Finalizing B afterward would still charge the client
a real second invoice for the same work, since `FinalizeInvoiceAsync` just PDFs/locks whatever B's own
`LineItems` already say, never rebuilds them.

While investigating this, the user also asked separately why a Fixed Fee didn't become billable again after
voiding one duplicate invoice, staff costs did. Checked live against the real Acme data: **not a bug** - Acme's
"ERP Migration Phase 2" project still had 5 *other* Finalized invoices carrying the same duplicate Fixed Fee
line, untouched by the one Void. `HasFixedFeeBeenInvoicedAsync` (from the fix above) correctly asks "does *any*
Finalized invoice for this project still carry the fee" - true, since 5 still did. Unlike T&M/Expense, which
lock to one *specific* invoice's own `InvoiceId` (so voiding invoice A only ever unlocks what A itself locked),
the Fixed Fee has no such per-invoice ownership - it's a project-wide "has this ever been billed" check. Voiding
every remaining duplicate would be needed to make the fee billable again - a data decision left to the user, not
auto-corrected.

- **`InvoicingService.GenerateDraftInvoiceAsync`** now rejects generating a draft for a period that overlaps an
  existing **Draft**-status invoice for the same client, unless it's an exact `periodStart` match (which is the
  pre-existing, legitimate "regenerate this same draft in place" path via `GetDraftAsync` - untouched,
  reordered only so the new check runs first and skips entirely when that reuse path applies, so refreshing
  your own open draft is never blocked). Deliberately **Draft-only**: a period overlapping an already-Finalized
  or Voided invoice is still allowed to generate - the per-line exclusions already shipped this session (T&M,
  Expense, Fixed Fee) correctly handle that case by just leaving already-billed work off the new draft, not by
  blocking generation outright.
- **New `IInvoiceRepository.GetOverlappingDraftAsync(clientId, periodStart, periodEnd, ct)`** - a plain
  range-overlap query (`start1 <= end2 && start2 <= end1`) scoped to `Status == Draft` only. No new persisted
  state, no migration - a pure additional read check before generation proceeds.
- **No Functions/Contracts/Angular changes needed at all** - `InvoicesFunctions.GenerateDraft` already wraps
  the call in `catch (InvalidOperationException)` → `BadRequestObjectResult`, and `invoicing-page.ts`'s
  `generateDraft()` already renders `err?.error?.error` into the existing error banner. `BillingRollForwardService`
  already catches `Exception` per client and counts it as a failure without aborting the run - a client with a
  lingering overlapping Draft now correctly shows up as a roll-forward failure instead of silently
  double-billing, with every other client unaffected.
- Backend build clean, 63/63 tests pass (4 new: overlapping Draft throws - confirmed this test genuinely fails
  without the fix by temporarily reverting the check and re-running before restoring, same rigor as the last
  two fixes; a non-overlapping Draft still succeeds and coexists; regenerating the exact same `periodStart`
  still refreshes in place, not blocked; overlapping only a Finalized invoice still succeeds, proving the block
  is Draft-only). Angular unaffected (no frontend change). API host restarted (backend logic change inside an
  existing method) - confirmed clean startup.
- **Live-verified via direct API calls against real Acme data**: generated a fresh Draft for a clean period,
  then confirmed a second draft request for an overlapping period was rejected with a real 400 and the exact
  intended message; confirmed regenerating the identical `periodStart` still returns the same invoice id
  (refresh-in-place untouched); confirmed a genuinely non-overlapping period still succeeds as a brand-new
  draft. Also confirmed in the browser itself: filled the form for the same overlapping period and clicked
  Generate Draft - the new red error banner rendered exactly as intended, no second draft created. Test drafts
  cleaned up via the app's own Delete Draft endpoint afterward.

## Done still later again in this session, 2026-09-10 — a third double-billing bug: Fixed Fee had no locking at all, and had already double/triple-billed a real client in this dev DB

Immediate follow-up to the Void/Delete feature above, reported live by the user right after: "I have just
generated an invoice for Acme, and the ERP Migration Phase 2 Fixed fee showed up on the next draft even though
it is already invoiced on a previous invoice and that invoice was not voided." Same bug class as the
double-billing fix two entries below (T&M/Expense), but for the third and last invoice line type - Fixed Fee -
which that earlier fix's own code comment explicitly, and it turns out incorrectly, exempted: "Fixed Fee line
items don't derive from either row type, so there's nothing to lock for those."

- **Confirmed this was real and already live in the dev DB, not a one-off**: queried Acme's own invoice history
  directly - `InvoiceGenerationService.BuildDraftAsync`'s Fixed Fee branch had **zero exclusion logic of any
  kind**, unconditionally adding the project's full `FixedFeeAmount` to *every single draft ever generated*,
  regardless of period or of how many times it had already been finalized. Acme's "ERP Migration Phase 2"
  project had its Fixed Fee billed on **8 separate real Finalized invoices** already sitting in this dev DB
  (`#3`, `rr`, `inv1233`, `inv1234`, `inv123456`, `uuuu`, `jhg`, plus 2 Voided ones) - a genuine, already-
  happened multiple-billing incident in what's meant to be this app's own demo data.
- **Confirmed the intended design from the FDD itself before fixing anything**: extracted the FDD docx's plain
  text again (same throwaway unzip-and-strip-tags approach as the earlier full re-audit) and found the decisive
  line: "A project is either a fixed one-off piece of time or repeating time on a monthly basis, controlled by
  the billing or invoice period held against the project." Fixed Fee is explicitly **one-off**, not recurring -
  confirming the bug's fix direction, and also matching `IRevenueRecognitionService`'s already-existing model
  (recognizes a Fixed Fee project's revenue once, total, prorated by hours-consumed-of-budget - never as a
  repeating full amount either).
- **New `IInvoiceRepository.HasFixedFeeBeenInvoicedAsync(projectId, ct)`** - true if any `InvoiceLineItem` of
  `Type == FixedFee` for that project sits on a **Finalized** invoice. Only Finalized counts (not Draft, not
  Voided) - matching the exact same "a Draft locks nothing yet" convention already established for
  TimesheetEntry/ExpenseEntry, which is also what makes regenerating the very Draft that already carries this
  project's fee safe (its own not-yet-cleared line item is on a Draft, so it doesn't trip its own check) without
  needing any self-exclusion logic. Voided is excluded so voiding correctly makes the fee billable again, same
  as the Void feature's own philosophy for T&M/Expense. `InvoiceGenerationService.BuildDraftAsync`'s Fixed Fee
  branch now checks this before adding the line; gained a new `IInvoiceRepository` constructor dependency (both
  already-registered Scoped services, no DI cycle).
- Backend build clean, 59/59 tests pass (3 new: the exact reported scenario - finalize a Fixed Fee invoice, then
  generate a second draft for a later period, assert the fee is excluded - **confirmed this test genuinely
  fails without the fix** by temporarily reverting the one-line guard and re-running before restoring it, same
  rigor as the T&M double-billing fix; regenerating the same still-open Draft correctly keeps its own fee, not
  stripped by its own not-yet-cleared line item; voiding the invoice correctly makes the fee billable again).
  Angular build unaffected (backend-only change, no DTO/contract shape change). API host restarted (new
  constructor dependency) - confirmed clean startup, all routes registered.
- **Live-verified directly against the real, already-affected Acme data, not a synthetic repro**: regenerated
  the stale overlapping-period Draft for Acme (`clientId 2`, `2026-08-31` to `2026-09-10` - a period that
  already had two Finalized invoices, `#jhg` and `#uuuu`, each carrying a full duplicate 68,000.00 EUR Fixed Fee
  line) via a direct `Invoices_GenerateDraft` API call - came back **0 line items, 0.00 total**, correctly
  excluding both the already-finalized Fixed Fee and the already-locked Data Quality Audit T&M/expense entries.
  Confirmed the same result in the UI after a refresh, then deleted that now-empty stale Draft through the app.
  Deliberately did **not** attempt to retroactively correct the 8 real over-billed Finalized invoices already in
  the dev DB (a business/accounting decision, not a code fix - each one is a real historical Finalized invoice;
  the new Void feature above is available if the user wants to correct any of them).
- **Not fixed, flagged as a separate, narrower known gap**: `FinalizeInvoiceAsync` never re-validates a Draft's
  *own already-stored* line items against current reality at finalize time - it locks/finalizes whatever the
  invoice already has, even if a sibling Draft for an overlapping period was finalized first and the first
  Draft's stored lines are now stale. This applies identically to T&M/Expense/Fixed Fee alike and predates this
  fix entirely; the fix here (and the earlier T&M/Expense one) closes the *generation-time* half of double-
  billing, which is what was actually reported and is the same boundary this codebase's existing locking design
  already draws. Worth a future look if a stale Draft is ever finalized without being regenerated first.

## Done even later still in this session, 2026-09-10 — Delete a Draft invoice; Void a Finalized invoice, so a mistake can actually be corrected

Immediate follow-up to the double-billing fix above: that fix closed the hole that let a duplicate invoice be
finalized in the first place, but the user hit exactly that scenario live before it landed and asked directly:
"I think I need the option to delete a draft, and even a finalized invoice, putting the entries back into an
invoicable state?" This runs against an explicit FDD design decision ("a Finalized invoice is never
un-finalized in this app"), so rather than quietly picking a shape, it was raised with the user directly via
Plan Mode with an `AskUserQuestion` round first.

- **Confirmed with the user**: a **Draft** gets a plain delete (nothing was ever locked for a Draft, so nothing
  needs unwinding). A **Finalized** invoice gets a **Void**, not a hard delete - the invoice row, its real
  `InvoiceNumber`, and its PDF are all kept permanently (a durable record this number was issued then
  corrected, closer to standard accounting practice than erasing it), and every `TimesheetEntry`/`ExpenseEntry`
  locked to it gets unlocked (`InvoiceId = null`) so it becomes invoiceable again.
- **A real DB-level gap found while planning, not just an app-level check**: `InvoiceConfiguration`'s partial
  unique index on `{ClientId, InvoiceNumber}` was filtered to `Status = 'Finalized'` rows only - if a voided
  invoice's status became `Voided` while keeping its number, the index would stop covering it, meaning the
  database itself would silently let a *new* Finalized invoice reuse a voided invoice's number, defeating the
  whole point of Void. Filter widened to `Status IN ('Finalized', 'Voided')` (new migration
  `AddInvoiceVoiding`); `InvoiceRepository.InvoiceNumberInUseAsync`'s app-level check updated to match
  (`Status != Draft` instead of `Status == Finalized`).
- **New `Invoice` fields** (`VoidedAtUtc`/`VoidedByUserId`/`VoidedByName`/`VoidReason`) - same "FK + denormalized
  name snapshot" pattern as `FinalizedAtUtc`/`FinalizedByUserId`, since this is specifically meant to be a
  durable audit record even if the voiding user is later renamed or deactivated. `InvoicingService.GetPdfAsync`'s
  guard inverted (`Status == Draft` blocks, not `Status != Finalized`) so a Voided invoice's PDF stays
  downloadable, same as a Finalized one's.
- **New `IInvoicingService.DeleteDraftAsync`/`VoidInvoiceAsync`** - `DeleteDraftAsync` throws if the invoice
  isn't a Draft; `VoidInvoiceAsync` throws if it isn't Finalized, then re-fetches everything currently locked to
  it via the real `InvoiceId` FK (`ITimesheetEntryRepository`/`IExpenseEntryRepository.GetByInvoiceIdAsync`,
  both new) and clears it on every one before flipping the status. New `Invoices_DeleteDraft`
  (`DELETE invoices/{id}`) and `Invoices_Void` (`POST invoices/{id}/void`) endpoints, both Admin-only,
  audit-logged (`Invoice.DraftDeleted`/`Invoice.Voided`) via `IAuditLogService` newly injected into
  `InvoicesFunctions`.
- **Frontend**: `invoicing-page` gained a "Delete Draft" link next to Finalize on Draft cards, and a Finalized
  card's action row now also carries a reason input + "Void Invoice" link (both `ConfirmService`-gated,
  `destructive: true`, mirroring `expenses-list-page`'s own delete pattern exactly); a Voided card shows
  "Download PDF" plus a plain-text "Voided {date} by {name}: {reason}" line, with no other actions - already
  terminal. New `statusBadgeClass()` helper (Finalized→green, Draft→amber, Voided→red) replaces the old
  two-way ternary on both `invoicing-page` and the read-only `my-invoices-page` (PM view), which would
  otherwise have rendered a Voided invoice with the same amber badge as a Draft.
- Backend build clean, 56/56 tests pass (4 new: `DeleteDraftAsync` removes the invoice and throws on a
  Finalized one; `VoidInvoiceAsync` unlocks every entry/expense and throws on a Draft). Angular build clean.
  API host restarted (new migration + endpoints + `IAuditLogService` DI change on `InvoicesFunctions`) -
  confirmed via a direct DB script that all 4 new `Invoices` columns and the widened index filter landed.
- **Live-verified end-to-end in the browser, both flows with real proof, not just the happy path**: deleted a
  genuine empty Draft for SVG - confirmed the "Draft invoice deleted." banner and that it vanished from the
  list. Voided the real `#CLEANUP-EXPENSE-LOCK` invoice for Everlast (the deliberate fix-through-the-app
  invoice left over from the double-billing round above, with one real locked expense on it) with a typed
  reason - confirmed the card flipped to a red "Voided" badge showing "Voided Sep 10, 2026 by Mark Llewellyn:
  Duplicate invoice from double-billing bug - correcting", "Download PDF" still worked (fetched it directly,
  200/`application/pdf`), and its expense line stayed visible read-only, no Void/Finalize controls. Confirmed
  the unlock was real, not just a status flip: queried the dev DB directly (0 rows still pointing `InvoiceId`
  at the voided invoice), confirmed the Expenses list now showed that same expense with Edit/Delete instead of
  "Locked (invoiced)", then generated a fresh Draft for Everlast covering the exact same period and got the
  same line back (125.00 USD, same staff/date/description) - proof it's genuinely re-invoiceable, not just
  unlocked in name. Finally tried finalizing that fresh draft under the exact same number,
  `CLEANUP-EXPENSE-LOCK` - correctly rejected with "Invoice number 'CLEANUP-EXPENSE-LOCK' is already in use for
  this client.", proving both the app-level check and the underlying DB index fix block reuse of a voided
  invoice's number.
- **New demo-data state**: invoice `#CLEANUP-EXPENSE-LOCK` is now `Voided` (was `Finalized`) rather than a new
  artifact - one of the SVG Drafts from the double-billing round's own leftover state was deleted as part of
  verification, and a fresh Draft for Everlast (2026-08-01 to 2026-09-10, 125.00 USD, never finalized) was left
  behind from the re-invoiceability proof.

## Done even later still in this session, 2026-09-10 — a real double-billing bug: invoicing never excluded already-invoiced entries

Found live by the user, immediately after the invoice-per-entry-line work below: they generated and finalized
an invoice, then generated a second draft for the *same client and an overlapping period* - and the exact same
already-invoiced entries appeared again, at full undiscounted value. If that second draft had also been
finalized, the client would have been billed twice for the same hours. Reproduced live first (a genuine second
Draft, full 12,000.00 USD, all 10 lines duplicated) before touching any code, to confirm this wasn't a
misunderstanding.

- **Root cause**: `ITimesheetEntryRepository.GetCountedForInvoicingAsync` - the query both `BuildDraftAsync` and
  `LockEntriesAsync` use to find a project's invoiceable entries for a period - filtered only by project, date
  range and `BillingPeriodChoice`. It never excluded an entry that was already locked to a *prior* invoice
  (`InvoiceId is not null`). This bug **predates this session's invoice-line-detail work entirely** - the old
  one-line-per-project rollup had exactly the same defect, just hidden inside a single summed number instead of
  ten visibly-repeated lines with real dates and descriptions, which is presumably why nobody had ever noticed
  it before.
- **Fixed at the source**: added `&& e.InvoiceId == null` to `GetCountedForInvoicingAsync`'s query - the single
  change that closes the whole class of bug, since both draft-generation and finalize-time locking share this
  one method. Added `.DistinctBy(l => l.ProjectId)` was already in place from the earlier per-entry-line work,
  no change needed there.
- **A second, equally real half of the same bug**: `ExpenseEntry` had **no locking mechanism at all** - no
  `InvoiceId` field, nothing stamped at finalize time, no exclusion anywhere. An already-invoiced expense could
  be re-included on a new invoice *and* still be freely edited or deleted through the Expenses page, forever.
  Fixed properly, not just patched: new `ExpenseEntry.InvoiceId`/`Invoice` (new migration
  `AddExpenseEntryInvoiceLock`), `ExpenseEntryRepository.GetBillableForProjectAsync` now excludes locked
  expenses (mirrors the TimesheetEntry fix exactly), `InvoicingService.LockEntriesAsync` extended to also stamp
  Expense-type lines at finalize time (re-runs `GetBillableForProjectAsync` per project, same "re-run the same
  query, no FK back to the original row" pattern already used for T&M entries), and `ExpenseEntriesFunctions`
  Update/Delete gained the exact same `InvoiceId is not null` → 409 lock guard `TimesheetEntriesFunctions`
  already had. New `ExpenseEntryDto.Invoiced` field; `expenses-list-page` now shows "Locked (invoiced)" instead
  of Edit/Delete for a locked expense, mirroring the Log Time grid's own existing locked-entry treatment.
- Backend build clean, 52/52 tests pass (2 new: one reproducing the exact reported scenario - finalize, then
  generate a second draft for the same period, assert it comes back empty - confirmed this test genuinely
  fails without the fix by temporarily reverting it and re-running before restoring; one for the equivalent
  expense-locking path, which had zero prior coverage of any kind). Angular build clean. API host restarted
  (new migration) - confirmed the `InvoiceId` column landed on `ExpenseEntries` directly against the dev DB.
- **Live-verified end-to-end, with a real scare along the way worth recording honestly**: the first live
  re-test through the actual UI still showed all 10 duplicate lines, which looked like the fix hadn't taken -
  turned out to be a stale/already-existing Draft invoice from the *reproduction* step earlier, combined with a
  UI click that didn't register cleanly (a recurring flakiness with this exact form already noted earlier this
  session). Called the `Invoices_GenerateDraft` endpoint directly (via script, not the UI) to get an
  unambiguous answer: it now correctly excluded all 9 already-locked timesheet entries, leaving exactly **one**
  line - a single pre-existing expense that had been invoiced *before* the expense-locking fix existed, so it
  was never retroactively stamped (expected - this codebase's established "only affects entries saved/
  processed from now on" convention, not a bug). Closed that one real remaining risk properly, through the app
  itself rather than a raw DB write: finalized that small draft too (`#CLEANUP-EXPENSE-LOCK`, 125.00 USD),
  confirmed the expense now shows "Locked (invoiced)" on the Expenses page, then generated one more fresh draft
  for the exact same client and period and confirmed it now comes back with **zero line items, zero total** -
  the double-billing risk is fully closed, not just mitigated.
- **New demo-data artifacts**: invoice `#CLEANUP-EXPENSE-LOCK` (a real, deliberate fix-through-the-app, not
  test noise) and one harmless empty ($0, 0 lines) leftover Draft invoice for Everlast from the final proof
  step - left as-is, matching this dev DB's existing density of small finalized test invoices for this client.

## Done later still in this session, 2026-09-10 — invoice line items: per-entry detail, closing the audit's biggest finding

Immediate follow-up to the fresh FDD re-audit below, picked as the user's first priority. The FDD says a
finalized invoice line shows "the staff member's name, the project name, the task date, the description, the
total hours, the rate and the amount" - the actual app (`InvoiceGenerationService.BuildDraftAsync`) rolled an
entire project's worth of entries for a billing period into **one single line** (`Description` = just the
project name, no staff/date/rate anywhere). Planned properly (Plan Mode, one deep Explore agent mapping the
whole invoicing pipeline, two AskUserQuestion rounds) before writing code, since it touched money-calculation
correctness and real UX tradeoffs.

- **`InvoiceGenerationService.BuildDraftAsync`** now creates one `InvoiceLineItem` per `TimesheetEntry` (Time &
  Materials) and one per billable `ExpenseEntry` (both confirmed with the user first - expenses explode too,
  not just time), instead of one summed line per project. Fixed Fee stays a single line (no natural per-entry
  shape for a flat fee, unchanged). `GetCountedForInvoicingAsync` already returned everything needed (User,
  Date, Description, resolved rate) - no repository query change required there, only
  `ExpenseEntryRepository.GetBillableForProjectAsync` needed a new `.Include(e => e.User)`.
- **New `InvoiceLineItem` fields**: `StaffId`/`StaffName` (FK + denormalized snapshot, same pattern as
  `TimesheetEntry.ApprovedByStaffId`/`ApprovedByName` - an invoice must stay reproducible even if the person is
  later renamed or deactivated), `TaskDate`, `Rate` (the entry's own `ResolvedCustomerRate`, snapshotted as-is
  in native currency, never re-resolved). New migration `AddInvoiceLineItemStaffAndTaskDetails` - confirmed
  applied directly against the dev DB (`PRAGMA table_info` showed all 4 new columns).
- **A real bug caught and fixed before it shipped**: the first design draft resolved the invoice's native
  currency via `project.Client.ReportingCurrencyCode` - but `IProjectRepository.GetByIdAsync` only
  `.Include(p => p.Client)`, never `.ThenInclude(c => c.Currency)`, so that property would have silently
  fallen back to "GBP" for every non-GBP-reporting client. Fixed by fetching the Client separately via
  `IClientRepository.GetByIdAsync` (which does include `Currency`), mirroring
  `ReportingService.ResolveNativeProjectCurrencyAsync`'s own identical existing pattern exactly - caught by
  reading that precedent before writing new code, not after a live bug.
- **Bulk "discount whole project" added**, confirmed with the user first: exploding one project-rollup line
  into many per-entry lines would otherwise force an admin to discount a project's dozens of lines one at a
  time instead of the one click it used to take. New `IInvoicingService.ApplyProjectDiscountAsync` (mirrors the
  existing single-line `ApplyLineItemDiscountAsync` exactly, just loops every line for one project) + new
  `Invoices_ApplyProjectDiscount` endpoint (`PUT invoices/{id}/projects/{projectId}/discount`).
  `LockEntriesAsync` gained a `.DistinctBy(l => l.ProjectId)` since there are now many T&M lines per project
  instead of one - same correctness, avoids re-running the identical lock query once per entry.
- **A latent PDF bug caught and fixed proactively, before it could surface for real**: the QuestPDF renderer
  put the running Total in `page.Footer()`, which QuestPDF repeats on *every* page - harmless at one line per
  project (invoices never spanned multiple pages), but exploding to per-entry lines makes a busy client's
  invoice genuinely multi-page, which would have printed "Total: X" at the bottom of every page. Moved the
  Total into the end of the flowing `page.Content()` instead (after all project groups), and grouped the line
  table itself by project (heading + subtotal per project, 6 columns: Staff/Date/Description/Hours/Rate/Amount)
  rather than one flat table mixing every project's entries together.
- **Admin invoicing UI** (`invoicing-page`) and the read-only PM view (`my-invoices-page`) both now render
  line items grouped by project (new shared `core/utils/invoice-line-grouping.ts`, used by both), matching the
  PDF's own new layout.
- Backend build clean, 50/50 tests pass (2 new: one confirming N entries produce N line items not 1, one for
  `ApplyProjectDiscountAsync`'s bulk recompute - existing tests needed no changes since they only ever seeded
  one entry per project and asserted on locking, never on line count). Angular build clean. API host restarted
  (new migration + endpoint) - confirmed the migration actually applied against the dev DB directly, not just
  "no error on apply".
- **Live-verified end-to-end with real proof, not just a UI glance**: generated a real Draft invoice for
  Everlast (D365 Migration, Time & Materials, USD reporting currency) covering 2026-08-01 to 2026-09-10 - got
  **10 real per-entry lines** (9 timesheet entries + 1 expense, confirmed the expense line correctly showed
  blank Hours/Rate) with real staff names, dates, descriptions and rates, not one rolled-up line. Applied the
  new bulk "discount whole project" control (10%) - confirmed every line's discount updated together and the
  invoice total recalculated correctly (12,000.00 → 10,800.00). Finalized it (`#MULTILINE-TEST-1`, invoice id
  16) and fetched its stored line items directly via the API (not the UI) to confirm the persisted shape
  matched exactly. **Verified the PDF without clicking the in-app "Download PDF" button** (per this session's
  own established rule) two ways: (1) fetched the real finalized PDF's raw bytes via a script and confirmed
  real `%PDF` magic bytes; (2) since that one real invoice only spans one page and can't prove the Total-per-
  page fix on its own, built a synthetic 80-line, 2-project invoice through the *actual*
  `QuestPdfInvoiceRenderer` class directly (a throwaway console script referencing the real Infrastructure
  project, no HTTP/token involved, cleaned up after) and extracted its text with PdfPig: a genuine 3-page PDF,
  **"Total:" appears exactly once** across all 3 pages (the old `page.Footer()` code would have printed it on
  all 3), and "Subtotal:" appears exactly twice, once per project group - definitive proof the fix works, not
  just a code-review guess. Also confirmed backward compatibility live: every pre-existing finalized invoice
  (`#jj`, `#gg`, `#76`, `#right`, `#12`, `#BLOBTEST-1`) still renders correctly in the UI with "-" placeholders
  for the new Staff/Date/Rate columns on their old rolled-up-shape lines - no error, no broken row.
- **New demo-data artifact**: invoice `#MULTILINE-TEST-1` (id 16) on Everlast, finalized during live
  verification - left as-is like every other real test invoice already in this dev DB (`gg`/`jj`/`right`/`76`/
  `12`/`BLOBTEST-1`), since a finalized invoice can never be un-finalized in this app (by design, per the FDD).

## Done later in this session, 2026-09-10 — a fresh full FDD re-audit, three parallel checks against the real code

Not a fix - the user asked for exactly this after the Project Detail Breakdown work below, rather than trusting
this file's own accumulated notes from past audits. Extracted the FDD's full text directly from the `.docx`
itself for the first time (`word/document.xml` + `word/comments.xml` via a throwaway unzip-and-strip-tags
script - no cached plain-text version existed anywhere in the repo before now), including embedded reviewer
comments that were never visible from HANDOFF's own past summaries. Three parallel checks (Clients/Projects/
Rates; Timesheet Entries/Invoicing/Payroll; Auth/Reporting/Notifications), each re-reading the real code fresh
rather than trusting what a past audit claimed was already done.

**Real, actionable gaps found** (all four now fixed, see the entries above - this numbered backlog is fully
closed):

1. ~~**Invoice line items didn't match the FDD's own spec**~~ - **fixed the same session**, see the entry above.
2. ~~**No "Team" concept exists anywhere in the app.**~~ - **fixed 2026-09-11, a later session**, see that
   session's own "Done" entry above. FDD: reporting "on a team, role and user basis" - role and user reporting
   both existed (`IReportingService`'s by-user/by-role breakdowns), but there was no Team entity in the domain
   model at all. User's own call once asked directly: "just make it a staff grouping like Roles, don't overthink
   it" - built as a literal mirror of the existing Role feature, including the by-Team Reports breakdowns.
3. ~~**No per-role rollup on the Estimated Cost/Profit panel.**~~ - **fixed 2026-09-11, a later session**, see
   that session's own "Done" entry above. FDD: "calculated per user *and per role*."
   `ProjectEstimateLine` already carries `RoleId`/`RoleName` per line - the data is there, just never grouped
   in the UI. Small, cheap fix, not started.
4. ~~**"Hours remaining" lives outside the Reporting feature.**~~ - **fixed 2026-09-11, a later session**, see
   that session's own "Done" entry above. FDD describes it as part of Reports/Export; it only existed as this
   session's separate Budget & Cost Status feature (`ActualHours`/`HoursUsedPercent` vs `BudgetHours`), never
   surfaced in the Reports page or the CSV export.

**Business decisions surfaced, not bugs** (the FDD document itself is ambiguous or self-contradictory here -
flagged for the user rather than silently picked either way):

5. **Should `Project.BudgetHours` reset per billing period** (e.g. monthly) for a repeating-billing project? A
   reviewer comment embedded in the FDD itself raises this ("Hours might be 800 a month and therefore the
   budget needs to reset each month") and nobody ever answered it. Confirmed via code: `BudgetHours` is purely
   an all-time lifetime cumulative figure everywhere it's read (`BudgetMonitoringService`,
   `ProjectStatusService`, `ITimesheetEntryRepository.GetActualsByProjectIdsAsync`) - no period concept
   anywhere. Not a literal FDD violation (the main requirements text never actually mandates a reset), but a
   real, still-open ambiguity. **Discussed with the user 2026-09-11, deliberately deferred, not built**: laid
   out the tradeoffs (a global behavior change would break one-off/Fixed-Fee projects where "reset" makes no
   sense; a per-project opt-in flag would be the safer shape if this is ever built) and recommended not building
   it speculatively, since only a reviewer comment - not the main FDD text - asks for it and no specific
   client/project need has surfaced. User agreed to leave `BudgetHours` as all-time/cumulative for now. Revisit
   if a real client contract actually needs a resetting allowance.
6. **Out-of-hours approval timing.** The FDD's main text and the built app both implement *post-hoc* approval
   (log first, an Admin/PM approves the already-logged entry before payroll aggregation -
   `TimesheetApprovalFunctions.Approve`). A reviewer comment in the same document argues for *pre-*
   authorization instead ("can't have guys just choosing to work in evenings at the expense of day") - a real
   tension inside the source document itself, not something to silently resolve either way. **Discussed with the
   user 2026-09-11, deliberately deferred, not built**: pre-authorization would be a materially new
   request-then-approve workflow (closer to a leave-request system than a tweak), touching the same
   entry-locking/payroll-timing logic this session already hardened against double-billing - not something to
   build speculatively off a single reviewer comment with no specific incident behind it. User agreed to keep
   today's post-hoc approval flow. Revisit if OOH abuse actually becomes a real, reported problem.

**Confirmed correct/complete this round** (re-verified fresh, not just trusted from a past audit): 5-tier rate
resolution, effective-dated `StaffCost`/`RateCard`, discount scope (`RateCard.DiscountPercent` +
`InvoiceLineItem.DiscountPercent` only, confirmed by grep - nowhere else), `ExpenseEntry`'s own `Currency`
field, per-entry `BillingPeriodChoice` UI, server-side validation (`TimesheetEntriesFunctions` alone has ~10
explicit checks), impersonation's audit trail (both the acting admin and the impersonated target are written to
`AuditLog`, confirmed at two independent call sites), inactive-user exclusion from pickers, the Staff screen's
filter set, and the CSV export's 3 date-range presets (plus an extra `LastYear` option beyond the FDD's stated
3 - additive, not a gap). The single-currency `Client` shape (vs the FDD's "charge in" + "invoice in") remains
the same already-accepted simplification from a past session, not re-litigated as new.

## Done even later still in this session, 2026-09-10 — Sign Out didn't redirect to the login screen

Not an FDD gap - a real, separate UX bug the user noticed while testing the work above. Root cause, confirmed
by reading the code: `app.html`'s "Sign out" button calls `currentUser.logout()` directly, which clears the
token/profile/impersonation state but never navigated anywhere - the route itself doesn't re-check `authGuard`
just because the token disappeared out from under it, so the user was left looking at whatever page they were
already on, now just showing a "Sign in" button in the header instead of their name.

- **`CurrentUserService.logout()`** now calls `this.router.navigate(['/login'])` as its last step, after
  clearing local state - `sessionExpiredInterceptor` was confirmed as the only other place a "logout" happens
  in this app, and it does its own separate `localAuth.logout()` + navigate, never calling
  `CurrentUserService.logout()` at all, so this change has exactly one caller (the Sign Out button) and can't
  affect that other flow.
- **Checked against this session's earlier `guestGuard` fix before landing this**: `logout()` clears the token
  *before* calling `navigate(['/login'])`, so by the time `guestGuard` evaluates `isSignedIn()` for the
  redirect, it's already `false` and the navigation goes through cleanly rather than bouncing back to
  `/timesheet`.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified by the user themselves**: confirmed clicking Sign Out now lands directly on the login screen.

## Done later still in this session, 2026-09-10 — Entry Flags search: no longer silently truncates past 25 results

Picked up the "Known loose ends" list's Entry Flags search report (own logged entries once missing from a
search) at the user's request. Re-read the whole search path fresh (`EntryFlagsFunctions.SearchEntries`,
`ITimesheetEntryRepository.SearchForFlaggingAsync`, `ApplyStaffClientProjectSearch`) and found the same thing
the original investigation did: no code path excludes the searcher's own entries, and for an Admin
`projectIds` stays `null` (no scoping at all). Asked the user for repro specifics (exact search term, roughly
how old the missing entry was) - none were available, so the bug itself remains unreproduced. Rather than leave
it as a dead end, closed the identified real weak point instead: the picker was silently capped at 25 results
(most-recent-first) with **zero indication** anything was cut, so if this was the cause, the user would have
had no way to know their entry simply lost a "most recent 25" tiebreak.

- **`EntryFlagsFunctions.SearchEntries`** now fetches `take + 1` (26, not 25) purely to detect truncation
  cheaply, without a separate `COUNT` query - `hasMore = fetched.Count > 25`. New
  `EntryFlagSearchResponseDto(Results, HasMore)` envelope replaces the previous bare array response.
- **`entry-flags-page`**'s picker now shows "Showing the 25 most recent matches - add more detail... to narrow
  the search" beneath the results whenever `hasMore` is true, so a truncated search is visible instead of
  silent - the actual, provable fix, even though the original report couldn't be reproduced to confirm it was
  really the cause.
- Backend build clean, 48/48 tests pass unchanged (no dedicated test - matches this codebase's established bar
  for a thin Functions-layer response shape change). Angular build clean. API host restarted (changed endpoint
  response shape) - confirmed clean startup.
- **Live-verified by the user themselves**: searched "test" in the raise-a-flag picker (a very common word in
  this dev data's entry descriptions) - confirmed the new hint appeared, proving a broad search does trip the
  25-result cap in practice, exactly the scenario this fix targets.
- This doesn't fully close the "Known loose ends" item below (the original report is still unconfirmed/
  unreproduced), but meaningfully improves it - struck through as "mitigated further" rather than "fixed",
  since the root cause was never actually confirmed.

## Done still later in this session, 2026-09-10 — Project Detail Breakdown: charts showing who's done what, and on what

Immediate follow-up to the Budget & Cost Status feature below - the user, reviewing it live, asked whether it
was worth drilling further: "see what has taken the time... a click through to a more detailed project page
showing pie charts and who has done what." Not an FDD gap - a user-driven feature request, planned properly
(Plan Mode, two Explore agents, a Plan agent, three rounds of AskUserQuestion) before writing any code, since
it touched new charting infrastructure and a real money-accuracy question.

- **New `IProjectBreakdownService`/`ProjectBreakdownService`** (`TimeSheet.Domain/Services/
  IProjectBreakdownService.cs`, `TimeSheet.Infrastructure/Services/`) - three independent breakdowns of a
  project's all-time actual logged entries: **by staff member** (hours/cost/revenue/profit each person has
  logged), **by entry type/category** (bucketing a null `EntryTypeId` as "Unspecified"), and **by calendar
  month** (hours trend). New `ITimesheetEntryRepository.GetAllCountedForProjectAsync` (all-time, unbounded -
  deliberately a new method rather than reusing the existing, currently-uncalled `GetCountedForProjectAsync`
  with sentinel min/max dates, matching this codebase's documented-intent style) feeds all three.
- **A real money-accuracy question surfaced and resolved before coding**: for a Fixed Project Cost project, a
  naive `hours × rate` per person would NOT sum to the project's real recognized revenue - confirmed by reading
  `ReportingService.GetProfitOnProjectReportAsync`, which already deliberately zeros each person's individual
  Billed amount for a Fixed Fee project and recognizes revenue only once at the whole-project level via
  `IRevenueRecognitionService.GetRecognizedRevenueAsync` (prorated by hours-consumed-of-`BudgetHours`). Asked
  the user directly rather than silently picking; they chose to match the existing Reports behavior. The new
  service does the same: zeroes per-person Revenue/Profit for a Fixed Fee project and returns one
  `RecognizedRevenueToDate` figure instead, shown as a summary line above the by-staff table.
- **A real bug caught during implementation, before it shipped**: the plan's first draft resolved the
  project's "native currency" via `project.Client.ReportingCurrencyCode` - but `IProjectRepository.GetByIdAsync`
  only `.Include(p => p.Client)`, not `.ThenInclude(c => c.Currency)`, so `ReportingCurrencyCode` (which reads
  `Currency?.CurrencyCode ?? "GBP"`) would have silently fallen back to "GBP" for every non-GBP-reporting
  client. Fixed by fetching the Client separately via `IClientRepository.GetByIdAsync` (which does include
  `Currency`), mirroring `ReportingService.ResolveNativeProjectCurrencyAsync`'s own identical pattern exactly.
- **New `Projects_Breakdown`** endpoint (`GET projects/{id}/breakdown`), same `RequireAdminOrProjectManager`
  gate as `Estimate`/`Status` - deliberately NOT built on the general Reports feature's
  `reports/cost-on-project`/`profit-on-project` endpoints, which are hard Admin-only app-wide and would wrongly
  block a PM from seeing their own project's figures here.
- **Chart.js added as a new npm dependency** (`chart.js` alone, not `ng2-charts` - zero framework peer-
  dependency, avoids a peer-conflict risk against this app's very new Angular 22, and only 3 chart instances
  ever exist on one page so `ng2-charts`'s extra diffing/change-detection integration buys nothing). New
  reusable `core/components/chart-canvas/chart-canvas.ts` - one generic wrapper (not a pie-specific +
  bar-specific pair, since Chart.js's `type` is just a config field), signal-`effect()`-driven, rebuilding the
  chart wholesale on any input change (fine - each chart is fetched once per page load, never live-updated).
  New `core/utils/chart-palette.ts` - a small fixed 10-color categorical palette (the app's own
  `--success`/`--warning`/`--destructive` tokens are only 3-4 colors, not enough for N staff members).
- **New `features/projects/project-detail-page/`** (route `/projects/:id/breakdown`, `authGuard` only - no
  `adminGuard`, so a PM linked in from My Projects can actually reach it, same pattern as `admin/entry-flags`/
  `admin/approvals`) - three sections (pie: by staff, pie: by entry type, bar: by month), each paired with a
  supporting table (money formatted `1.2-2`, hours `1.1-1`, matching the Estimate table's existing convention).
  Linked from all three places a project's status is already visible: `projects-all-page`/`projects-list-page`
  (new "Breakdown" action link per row), `project-edit-page`'s Budget & Cost Status panel ("View Breakdown"),
  and `my-projects-page` (new action column).
- Backend build clean, 48/48 tests pass unchanged (no dedicated test for the new service - matches this
  codebase's established bar, same as `ProjectStatusService`/`ProjectEstimateService`). Angular build clean
  (`chart.js` lazy-loads inside the new page's own chunk, not the initial bundle). API host restarted (new
  Function definitions + DI change) - confirmed clean startup.
- **Live-verified by the user themselves in the browser**: confirmed the new page renders correctly from all
  three entry points. Flagged that the "By Entry Type" pie showed "Unspecified" on every project checked -
  investigated via a throwaway console script (`Microsoft.Data.Sqlite` direct against the dev DB, cleaned up
  afterward) rather than guessing: confirmed this is accurate, not a bug - on "ERP Migration Phase 2" (the
  *only* project with any `EntryType` rows configured at all), just 1 of 33 entries (2 of 101.4 hours) actually
  has an `EntryTypeId` set, so "Unspecified" at ~98% is correct, just easy to miss the sliver next to it. Every
  other project has zero `EntryType` rows, so 100% "Unspecified" there is also correct. User declined to seed
  more demo data to make the chart look busier - left as accurate, real data.

## Done later in this session, 2026-09-10 — Budget & Cost Status: an "is this project on track" indicator

Not an FDD gap - a feature request, prompted by the user noticing there was "nowhere to see the current status
of a project" beyond the existing over-budget flag/notification. Deliberately distinct from the old, removed
"Project Health" feature (an AI-judgment call, removed 2026-09-03 for being unverified/costly) - this is purely
two already-stored, objective figures made visible: actual hours vs `Project.BudgetHours`, and actual cost vs
`Project.FixedFeeAmount`. Two design questions were asked and confirmed with the user before building: (1)
show it both as a compact badge on every project list row AND a fuller panel on the project detail page - not
just one or the other; (2) a Project Manager should see this for their own managed project too, not just Admin
(matching the existing Estimated Cost/Profit panel's visibility rule).

- **New `IProjectStatusService`/`ProjectStatusService`** (`TimeSheet.Domain/Services/IProjectStatusService.cs`,
  `TimeSheet.Infrastructure/Services/`) - `HoursUsedPercent`/`CostUsedPercent`, null when there's nothing to
  compare against (no `BudgetHours`/`FixedFeeAmount` set) rather than a misleading 0%. New
  `ITimesheetEntryRepository.GetActualsByProjectIdsAsync` - one bulk query (`GROUP BY ProjectId`, same
  batching shape as `IStaffProjectRepository.GetActiveAssignmentCountsAsync`) rather than one round trip per
  project on a list page.
- **New `Projects_Status`** endpoint (single project, Admin-or-PM via the existing
  `RequireAdminOrProjectManager`, mirroring `Projects_Estimate` exactly) for the detail panel; `Projects_ListAll`/
  `Projects_ListByClient` (both Admin-only, unchanged gating) now bundle bulk-computed status fields directly
  onto `ProjectDto` for the list badges - same "merge onto the existing DTO" pattern `AssignedStaffCount`
  already uses. The 4 new `ProjectDto` fields are nullable and only ever populated by these Admin/PM-safe
  endpoints - left null on `Get`/`Create`/`Update`/`ListAssignedToMe`, which a regular assigned staff member can
  also reach and must not see project financials through.
- **New `Projects_ListManagedByMe`** endpoint (any signed-in user; empty for anyone who manages nothing) - the
  PM-facing counterpart to the Admin-only list endpoints, since neither of those routes was reachable from the
  frontend by a non-admin. Powers a new **`my-projects-page`** (route `/my-projects`, `authGuard` only, no
  `adminGuard` - mirrors `my-invoices`'s exact "backend scopes, route stays open" pattern), with a new "My
  Projects" nav link shown when `isProjectManager() && !isAdmin()` **or** when actively impersonating someone
  (an Admin impersonating a PM can now check what that PM manages too) - also wired `onBehalfOfUserId` through
  this endpoint via the existing shared `ImpersonationAuthorization.ResolveViewTargetAsync` gate.
- **New shared `core/components/project-status-badges/`** (compact list-row badge, up to two pills - "Hours
  X%"/"Cost X%", or a neutral "No budget set") and **`core/utils/project-status.utils.ts`** (`75%`/`100%`
  green/amber/red banding, reusing the existing `badge-success`/`badge-warning`/`badge-danger` CSS classes -
  deliberately independent of the Settings-configurable notification thresholds, which drive when a
  notification *fires*, not this purely visual indicator). Badges added to `projects-all-page`/
  `projects-list-page`'s existing tables (new "On Track" column, distinct from the pre-existing Active/Inactive
  "Status" column) and `my-projects-page`. A fuller "Budget & Cost Status" panel (two progress bars) added to
  `project-edit-page`, right above the existing Estimated Cost/Profit panel.
- **A real template bug caught and fixed during live verification**: Angular parses `a ?? b | pipe` as
  `(a ?? b) | pipe`, not `a ?? (b | pipe)` - `{{ s.fixedFeeAmount ?? '?' | number: '1.2-2' }}` was silently
  passing the literal string `'?'` through the `number` pipe when `fixedFeeAmount` was null, breaking the whole
  interpolation (the "of ? fixed fee" text vanished entirely, not just showing garbage). Fixed by moving the
  null case into an `@if` block instead of relying on `??` before a pipe.
- Backend build clean, 48/48 tests pass unchanged (no dedicated test - matches this codebase's established bar
  for thin composition services, same as `ProjectEstimateService`). Angular build clean. API host restarted
  (new Function definitions + DI changes, twice this round) - confirmed clean startup each time.
- **Live-verified with a real before/after wherever a fix was involved, not just the happy path**: as Mark
  Llewellyn (Admin), confirmed the "On Track" column on both Admin project lists shows real, meaningful
  badges - "Notif Threshold Test" at a genuine 110% in red, "ERP Migration Phase 2" (Fixed Cost) showing both
  "Hours 17%"/"Cost 9%", projects with no budget showing the neutral badge. Opened the Fixed-Cost project's
  detail panel and confirmed the progress bars/percentages matched the list badges exactly. The `?? | pipe`
  template bug above was actually caught this way - live-verifying a no-budget project (Hours 110% over, no
  Fixed Fee Amount set) showed the broken "Cost: 495.00" with nothing after it; fixed the template, reloaded,
  confirmed it now reads "Only tracked for a project with a Fixed Fee Amount set above." - a real broken-then-
  fixed screenshot pair, not just a code-review guess. Confirmed My Projects works correctly both empty (Mark himself
  manages nothing) and populated (while impersonating Sarah Chen, the nominated PM on ERP Migration Phase 2) -
  the nav link only appeared during impersonation or for a real non-admin PM, exactly as designed.

## Done later in this session, 2026-09-10 — /login double-render fixed, closing a known loose end

Not an FDD gap - the "Known loose ends" list's newest, not-yet-resolved item, picked from a short menu of
options the user chose from. Root cause, confirmed by reading the code: `app.html` (the app shell) always
renders its header - nav links, "Acting as X" banner, etc - regardless of route, gated only by
`currentUser.isSignedIn()`/`isAdmin()` internally, not by which route is active. The `login` route itself had
no guard at all, so navigating straight to `/login` while already signed in rendered the full signed-in header
*and* the login form (from `<router-outlet>`) at the same time.

- **New `guestGuard`** (`core/auth/guest.guard.ts`) - the mirror image of `authGuard`: redirects to `/timesheet`
  if `LocalAuthService.isSignedIn()`, otherwise allows the route. Applied only to the `login` route in
  `app.routes.ts` - `bootstrap-local` and `auth/complete` (the SSO callback) were left untouched, out of scope
  for what was actually reported.
- **Checked the one flow this could plausibly break before applying it**: `sessionExpiredInterceptor` (from the
  2026-09-09 session) navigates to `/login?error=session_expired` after a 401, but it calls
  `localAuth.logout()` *synchronously, before* the `router.navigate(...)` call - so by the time `guestGuard`
  runs, `isSignedIn()` is already `false` and the guard correctly lets the redirect through rather than bouncing
  it back to `/timesheet`.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified both paths, with real proof for each**: (1) as Mark Llewellyn (Admin, still signed in from
  cold-navigating around during the adminGuard fix above), cold-navigated straight to `/login` - immediately
  redirected to `/timesheet` showing his own real data, no login form ever rendered. (2) Re-ran this session's
  own token-tampering trick (appended garbage to the stored JWT via a script, leaving the expiry untouched) and
  navigated to `/timesheet` - the 401 correctly redirected to `/login?error=session_expired` showing "Your
  session has expired - please sign in again.", proving `guestGuard` does *not* block the interceptor's own
  redirect. Confirmed via script that `localAuthToken`/`localAuthTokenExpiry` were both actually cleared from
  storage (not just visually hidden), matching the original 2026-09-09 verification of the same mechanism. The
  user signed back in via SSO afterward - confirmed the app came back up completely normally.
- This closes the "New, not resolved" login double-render item from "Known loose ends" below.

## Done in this session, 2026-09-10 — finished an unfinished, uncommitted fix left in the working tree: adminGuard could bounce a genuine Admin on a cold page load

Not an FDD gap — a real, separate app-level bug. Found `current-user.service.ts` sitting modified but uncommitted at the start of this session, with no HANDOFF note at all: a `meLoaded` promise had been added to `CurrentUserService` but never wired up — `loadMe()` never resolved it, and `admin.guard.ts` was still fully synchronous. Read the code to work out the intent: `adminGuard` reads `currentUser.isAdmin()` synchronously, but on a cold page load (fresh URL, bookmark, browser refresh) the constructor's own `/api/me` fetch hasn't resolved yet, so a genuine Admin hitting an admin route directly would get bounced to `/` because `isAdmin()` was still `false` at that instant. (Navigating to an admin route *from within* the already-running SPA was never affected — by then `/api/me` has long since resolved.)

- **`current-user.service.ts`**: `loadMe()` now calls the pre-existing-but-unwired `resolveMeLoaded()` in both the success and error subscribe callbacks, so `meLoaded` always settles once the initial `/api/me` call finishes one way or the other (it already resolved immediately in the constructor when there's no session to load at all).
- **`admin.guard.ts`**: now `async`, `await`s `currentUser.meLoaded` before reading `isAdmin()`. `authGuard` needed no change — it only checks `LocalAuthService.isSignedIn()`, a synchronous token-expiry check with no `/api/me` dependency.
- Angular build clean.
- **Live-verified with a real before/after, not just the after-state**: started Azurite, the API host, and Angular fresh for this session (nothing was running at the start). Signed in as Mark Llewellyn (Admin) via SSO, confirmed full Admin nav. Cold-navigated (full browser `navigate`, not an in-app link — the exact scenario this bug needed) straight to `/admin/users` — landed there correctly. Then, to prove this was a genuine fix and not something that already worked: temporarily reverted `admin.guard.ts` back to synchronous, let `ng serve` recompile, and repeated the exact same cold navigation to `/admin/users` — **reproduced the bug live**, landing on `/timesheet` instead. Restored the fix, let it recompile again, repeated the same cold navigation a third time — back to landing correctly on `/admin/users`. Confirmed the working-tree diff afterward matched exactly what was intended (the revert/restore round-trip left no residue). Did not additionally re-test the non-admin-blocked path, since that half of `adminGuard`'s logic (`isAdmin() ? true : redirect`) was untouched by this change — only the timing of when it runs changed.
- All three dev processes (Azurite, API host, Angular) left running at the end of this session.

## Done later still in this session, 2026-09-09 — closed the attachments-under-impersonation gap the moment it was actually hit

User impersonated Priya Patel, tried to upload an attachment to a new expense, and got a generic "Could not
upload the attachment." error - exactly the pre-existing, deliberately-left-alone gap flagged in this file's
"Your Overview"/"Expenses" impersonation entry above (entry + expense attachments never had any impersonation
support - `entry.UserId != user.UserId` fails outright when the caller is an Admin impersonating the entry's
real owner). That entry said fixing only one side would create a new asymmetry, so now that it's a real,
reported failure, both sides are fixed together rather than patching just the expense case.

- **`ExpenseAttachmentsFunctions`** (Upload/Download/Delete) and **`AttachmentsFunctions`** (the timesheet-entry
  equivalent) both now route their ownership check through the same shared `ImpersonationAuthorization` gate
  every other entry/expense endpoint already uses (`CheckOwnership` + `ValidateImpersonationTargetAsync`) -
  `onBehalfOfUserId` travels as a query-string param on all three actions, since Upload's body is multipart form
  data (not JSON) and can't carry it any other way. Both classes gained the `IUserRepository` constructor
  dependency the shared gate needs.
- **Frontend**: `ExpenseEntriesService`/`TimesheetEntriesService`'s `uploadAttachment`/`downloadAttachment`/
  `deleteAttachment` methods all gained an `onBehalfOfUserId` parameter (query string, same pattern as every
  other impersonation-aware call in this codebase); `add-expense-page.ts`/`add-entry-page.ts` now pass
  `this.impersonation.actingAs()?.id` through to all three.
- Backend build clean; Infrastructure.Tests 48/48 pass unchanged (no dedicated test for these Functions classes,
  same established bar as the other impersonation-gate wiring above). Angular build clean. API host restarted
  (new constructor dependency on both attachment Functions classes) - confirmed clean startup.
- **Live-verified end-to-end, reproducing the exact reported scenario**: impersonated Priya Patel, created a
  real expense against her own project (BrightPath Retail Group / POS Rollout - confirmed the dropdown correctly
  showed only her assigned projects), used Save & Attach, and uploaded a real file - it appeared in the
  Attachments list with real metadata (filename, timestamp, size), where before this fix the exact same steps
  produced "Could not upload the attachment." Deleted the attachment through the UI (also impersonation-gated,
  also worked), deleted the test expense entry itself, then reverted to Mark - his own Expenses list was
  completely unaffected throughout, no artifact left anywhere.

## Done even later still in this session, 2026-09-09 — the impersonation banner was unreadable in dark mode

User reported the "Acting as X" banner (from the impersonation work above) was hard to read in dark mode.

- **Root cause**: a genuine color-token bug in `styles.scss`, not specific to the banner component itself.
  `:root.dark`'s `--warning-foreground` was set to `oklch(0.141 0.005 285.823)` - the *exact same* near-black
  value as dark mode's own `--background`/`--foreground`(inverted) tokens, clearly a copy-paste leftover from
  `--success-foreground`'s dark-mode value (which is correct *there* only because `--success` is used as a
  solid, bright badge background needing dark text - `.banner-warning` and `.badge-warning` instead use
  `--warning` as a ~18% tint over the page's own dark background, which stays dark, so pairing it with a
  near-black text color made the banner text almost invisible - dark-on-dark. Light mode's `--warning-foreground`
  (a proper dark amber-brown, correct for a light tinted background) was never affected.
- **Fixed** by giving dark mode its own proper light, warm value: `oklch(0.9 0.1 75)` - same hue family as
  `--warning` itself, but light enough to read clearly against the dark-tinted banner background, mirroring how
  `--destructive`/`--success` already scale appropriately between the two color schemes.
- **Scope**: this one token is also used by `.badge-warning` (not just `.banner-warning`) - both get the fix
  automatically since it's a single shared CSS custom property, not a per-component style.
- Angular build clean. Frontend-only, CSS-only change - no backend/DB change, no API host restart needed.
- **Live-verified in the browser, both color schemes**: impersonated Sarah Chen in dark mode - the banner text
  ("Acting as Sarah Chen - new timesheet entries will be logged on their behalf.") now renders in a clearly
  legible warm amber against the dark background, a stark contrast to the earlier near-invisible dark-on-dark
  text. Switched to light mode with the same impersonation still active - confirmed completely unchanged
  (dark-brown text on the light amber tint, exactly as before this fix), proving the dark-mode-only token change
  didn't regress light mode.

## Done still later again in this session, 2026-09-09 — "Your Overview" and "Expenses" now honor impersonation too

Immediate follow-up to the Log Time/Calendar impersonation-refresh fix above - the user tried the picker more
thoroughly and reported "Your Overview" and "Expenses" still showed the Admin's own data while impersonating,
not the target user's. Confirmed by reading the code: unlike `TimesheetEntriesFunctions`, neither
`MeOverviewFunctions` nor `ExpenseEntriesFunctions` had ever had any impersonation support at all - every query
was hardcoded to the caller's own `UserId`, so this wasn't a refresh-reactivity bug like the one above, it was a
genuine missing feature on two of the FDD's own "on behalf of" surfaces.

- **New shared `ImpersonationAuthorization` static class** (`TimeSheet.Api/Auth/`) - extracted
  `TimesheetEntriesFunctions`'s own four private impersonation-gating methods (`ParseOnBehalfOfUserId`,
  `ResolveViewTargetAsync`, `ValidateImpersonationTargetAsync`, `CheckOwnership`) into one shared, generic
  (owner-id-based rather than entity-typed) location, since the exact same "only Admins may act on behalf of an
  active user" rule (the FDD's own words) now needs to be enforced identically in three places, not one -
  duplicating security-critical authorization logic across files risks one copy quietly drifting from the
  others. `TimesheetEntriesFunctions` itself was refactored to call the shared version (mechanical, behavior-
  preserving - same checks, same error messages, just relocated) rather than left with its own copy alongside
  two new ones.
- **`ExpenseEntriesFunctions`** now honors `onBehalfOfUserId` on List (view gate), GetById/Update/Delete
  (ownership gate, matching Duplicate's pattern on the timesheet side), and Create (an Admin logging an expense
  on behalf of someone else, mirroring `TimesheetEntriesFunctions.Create`'s identical branch) - the project-
  assignment check on Create/Update now correctly validates against the *impersonated* user's assignments, not
  the Admin's own (a latent bug in the pre-existing Update code, which checked `user.UserId` instead of
  `entry.UserId` - harmless before since the two were always equal, now they can differ). "Contract" kind
  entries are deliberately excluded from impersonation entirely - they're the Admin's own value entry, never
  tied to a staff member, so `OnBehalfOfUserId` is simply not read on that branch. New
  `CreateExpenseEntryRequest.OnBehalfOfUserId`/`UpdateExpenseEntryRequest.OnBehalfOfUserId` contract fields.
- **`MeOverviewFunctions`** now takes the same `onBehalfOfUserId` query param via `ResolveViewTargetAsync`,
  showing the impersonated user's own per-project hours/payroll breakdown instead of the Admin's.
- ~~**Deliberately NOT touched**: expense attachments (`ExpenseAttachmentsFunctions`) - checked the timesheet-
  entry equivalent (`AttachmentsFunctions`) first and found it *also* has zero impersonation support (a
  symmetric, pre-existing gap on both entity types, not something this round introduced or was asked to fix).
  Fixing attachments-under-impersonation for expenses only would create a new asymmetry rather than close an
  existing one, and wasn't part of what was reported - left as a possible future follow-up if it's ever asked
  for on both.~~ - **fixed later the same session, 2026-09-09**: the user hit this exact gap for real
  (impersonating Priya Patel, uploading an expense attachment) - see that session's own "Done" entry further
  down for both `ExpenseAttachmentsFunctions` and `AttachmentsFunctions` gaining full impersonation support
  together.
- **Frontend**: `expenses-list-page.ts` and `my-overview-page.ts` both gained the identical reactive
  `effect()`-on-`impersonation.actingAs()` pattern from the Log Time/Calendar fix above (refetch on switch, not
  just on initial load). `add-expense-page.ts` now threads `this.impersonation.actingAs()?.id` through
  `getById`/`create`/`update` and the "assigned projects" fetch, mirroring `add-entry-page.ts`'s existing
  pattern exactly. `ExpenseEntriesService`/`MyOverviewService` gained the matching `onBehalfOfUserId` params
  (query string for GET/DELETE, request body field for POST/PUT) - same shape as `TimesheetEntriesService`.
- Backend build clean; Infrastructure.Tests 48/48 pass unchanged (no dedicated test for these Functions classes
  either before or after - matches this codebase's established bar for thin CRUD/authorization-gate endpoints,
  same as `TimesheetEntriesFunctions` was never covered by a dedicated Functions-level test). Angular build
  clean. API host restarted (new `IUserRepository` constructor dependency on two Functions classes) - confirmed
  clean startup, all `ExpenseEntries_*`/`Me_Overview` routes registered.
- **Live-verified end-to-end**: as Mark Llewellyn (Admin), captured his own baseline (Your Overview: 87.7 work
  hours; Expenses: his own 3 entries), then impersonated Sarah Chen *while already on each page* - both
  immediately switched to her data (Your Overview: 122 work hours across her real projects; Expenses: her one
  real "Client site lunch" entry) with no reload. Created a brand-new test expense while impersonating her
  (Acme Manufacturing GmbH / ERP Migration Phase 2, 7.50 GBP) - the Client/Project dropdowns correctly showed
  only *her* assigned projects (just Acme), not Mark's full multi-client list, confirming
  `listAssignedToMe(onBehalfOf)` picked up the impersonation id too. The new entry appeared correctly in her
  list, was deleted through the UI (exercising the impersonated-delete authorization path), then reverted to
  Mark - his own Expenses/Overview came back exactly as they were before, with no trace of the test entry ever
  having existed there. Full round-trip proof, not just a UI glance.

## Done still later in this session, 2026-09-09 — impersonation didn't refresh Log Time/Calendar when switched mid-page, plus a real regression it surfaced

User asked a clarifying question about the "Log time on behalf of another user" feature (confirmed against the
FDD's own wording that its narrow scope - timesheet entries only, not a full account switch - is correct, not a
gap), then reported: "When I switch to impersonate another user I still only see my own log entries." Confirmed
by reading the code, not by guessing - a real bug, not a misunderstanding.

- **Root cause**: `log-time-page.ts` and `calendar-page.ts` both only call their own `refresh()` from the
  constructor and from explicit UI actions (search, month navigation, post-edit reload) - neither ever reacts
  to `ImpersonationService.actingAs()` changing on its own. The impersonation picker lives in the shared header
  (`app.html`) and doesn't navigate away/back when you pick someone, so switching who you're acting as while
  already sitting on Log Time or Calendar left the grid/calendar showing your own entries until some unrelated
  action happened to trigger a refresh - exactly the symptom reported. (Navigating to Log Time/Calendar *after*
  already impersonating worked fine all along, since the component's constructor runs fresh in that case - the
  bug only bit you when switching while already on one of those two pages.)
- **Fixed both pages** with an `effect()` in the constructor that reads `impersonation.actingAs()` and calls
  `refresh()` inside `untracked()` - the effect runs once immediately (replacing the old direct `this.refresh()`
  call) and again every time impersonation changes, without also re-running on unrelated signals `refresh()`
  happens to read (`searchText` on Log Time), which already have their own explicit refresh triggers.
- **A real, separate regression found and fixed while investigating**: while chasing why the impersonation
  picker's admin-only visibility seemed inconsistent, found that `/api/me` silently never resolves on a *fresh*
  page load when a valid session token already exists in storage (as opposed to signing in through the SPA's
  own login form, which works fine) - the nav bar would render in a reduced, non-admin-looking state even for
  Mark's own real Admin account, until some other action happened to "unstick" it. Confirmed the exact cause via
  a temporary console diagnostic (removed before landing the fix, see below) rather than guessing: this
  session's own earlier `sessionExpiredInterceptor` (added in the 401-handling round above) injects
  `CurrentUserService` - but `CurrentUserService`'s own constructor calls `/api/me` via `loadMe()` on every app
  boot with an existing token, and that request flows through the same interceptor chain. Asking the injector
  for `CurrentUserService` from inside the interceptor while `CurrentUserService` is itself still mid-
  construction is a genuine circular dependency (`NG0200`) - silently caught by the HTTP error channel (not an
  uncaught exception), so it never surfaced as a visible error, it just meant `/api/me` quietly never completed
  on that one specific bootstrap path.
- **Fixed** by having `sessionExpiredInterceptor` depend on `LocalAuthService`/`ImpersonationService` directly
  instead of `CurrentUserService` - neither of those has any HTTP-triggering constructor logic, so there's no
  cycle. Functionally identical outcome (clears the token, stops impersonation, redirects to login) for the
  401-handling behavior itself; only the dependency shape changed.
- Angular build clean throughout. Frontend-only changes, no backend/DB change, no API host restart needed.
- **Live-verified end-to-end, both fixes**: (1) confirmed the `NG0200` diagnostic fired on a cold reload before
  the fix and was gone after, and that a fresh `/timesheet` load now shows the full Admin nav (Approvals, Entry
  Flags, Payroll Periods, Invoicing, Reports, the impersonation icon) immediately, matching a real `/api/me`
  fetch confirming `role: "Admin"`. (2) As Mark Llewellyn (Admin), already sitting on Log Time showing his own
  entries (#126-128 etc.), opened the impersonation picker and picked Sarah Chen *without navigating away* - the
  grid immediately updated to her entries (#29, #42, #28...) with the "Acting as Sarah Chen" banner, no reload.
  Repeated on Calendar (already showing Mark's September, 29h) - switching to Sarah Chen immediately dropped the
  month total to 0h (her real entries are all in August). "Revert to myself" immediately restored Mark's own
  29h with no reload either. No test data was created - impersonation was only switched and reverted, never
  used to log/edit anything.
- Chrome connectivity was flaky again this session (had to be reconnected once more) - purely an extension
  connectivity issue, unrelated to any of this session's code changes.

## Done later in this session, 2026-09-09 — global 401 handling, closing the stale-session UX gap found while verifying the checkbox above

Immediate follow-up to the isBillable checkbox round below: while live-verifying it, a stale JWT in
localStorage 401'd and the app just sat there showing Add Expense's own "Could not load your assigned
projects" error banner instead of anything indicating the session itself was the problem - looked like a real
data bug before checking Network and finding the 401s. Not an FDD gap, but a real, generalizable app-level one
asked to be looked at as a follow-up.

- **Root cause, confirmed by reading the code, not guessed**: there was no global 401 handling anywhere.
  `LocalAuthService.isSignedIn()` only compares the client-stored expiry timestamp against the clock - it never
  actually confirms the token is still valid server-side. So *any* reason a token stops validating (genuine
  expiry the client failed to notice, or - the likely actual cause of the original incident, per `Program.cs`'s
  own existing comment - the API host being launched in a way that doesn't pick up `LocalAuth:JwtSigningKey`
  from `local.settings.json`, generating a fresh ephemeral key for that process only) leaves the app looking
  "signed in" (nav bar, cached `/api/me` profile) while every page's own API calls quietly fail, each with its
  own generic, misleading error message.
- **New `sessionExpiredInterceptor`** (`core/auth/session-expired.interceptor.ts`) - catches any `401` from an
  authenticated call, calls the already-existing `CurrentUserService.logout()` (already does full cleanup:
  clears the token, the cached `/api/me` profile, stops impersonation - reused as-is, no new cleanup logic
  needed), then redirects to `/login?error=session_expired`, reusing the login page's existing `?error=...`
  message-map pattern (`ERROR_MESSAGES`) rather than inventing a new mechanism. Registered in `app.config.ts`
  alongside the existing token-attaching `localAuthInterceptor`.
- **Explicitly excludes the login endpoint itself** (`req.url.endsWith('/auth/local-login')`) - a 401 from
  *that* call means "wrong password," not "your session expired," and must keep surfacing the login page's own
  existing error message untouched, not get reinterpreted as a session-expiry redirect.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified end-to-end, both the fix and its one edge case**, once Chrome reconnected (it dropped out
  twice this session - see below):
  1. Signed in normally as Mark Llewellyn (Admin), confirmed Log Time loaded real data.
  2. Used a throwaway script to tamper the stored token in place (appended garbage to the JWT string, leaving
     the stored expiry timestamp untouched/still-valid) - this reproduces the exact "looks signed in, fails
     server-side" scenario without needing to actually wait 8 hours or restart the API host mid-session.
     Reloaded the page (forces `LocalAuthService` to re-read the now-tampered token from storage) and confirmed
     it **automatically redirected to `/login?error=session_expired`** showing "Your session has expired -
     please sign in again." - the exact behavior this was built for. Confirmed via a second script call that
     `localAuthToken`/`localAuthTokenExpiry` were both actually cleared from storage, not just visually hidden.
  3. Signed back in for real afterward (the user typed their own credentials again) - confirmed the app came
     back up completely normally with no lingering broken state.
  4. **Checked the one deliberate exclusion doesn't regress the existing bad-password flow**: while still
     signed in with a real session, submitted the login form with a bogus email/wrong password from `/login` -
     confirmed the request 401'd but the page showed its own pre-existing "Invalid email or password." message
     (not the new session-expired one), and the still-valid real session in the header was completely
     untouched - proving the endpoint-exclusion check works and doesn't misfire on this adjacent case.
- **Chrome connectivity was flaky throughout this whole session** (disconnected/reconnected three separate
  times across both rounds of work) - not a code issue, just noted here in case it recurs for whoever picks
  this up next.
- **Noticed but not fixed, out of scope for this round**: navigating directly to `/login` while already
  signed in shows both the signed-in header *and* the login form at the same time (seen while testing the
  bad-password case above) - a separate, pre-existing minor UX quirk, unrelated to the 401-handling gap this
  round actually targeted.

## Done in this session, 2026-09-09 — Expense "Rechargeable to Client" checkbox, closing the last loose end from the Expense Attachments round

New session, picked up from HANDOFF's own note: the 2026-09-08 Expense Attachments work explicitly flagged
`ExpenseEntry.IsBillable` (the same FDD line as the attachments ask — "an indication of whether it is
rechargeable to the client") as a real, smaller gap it deliberately left alone — the field existed end-to-end
in the backend (`CreateExpenseEntryRequest`/`UpdateExpenseEntryRequest`/`ExpenseEntryDto` all already carried
it, `ExpenseEntriesFunctions` already read/wrote it with zero extra logic needed) and the new Expenses list
page already displayed a "Billable" column, but nothing let a user actually set it — Create silently hardcoded
`true` and there was no checkbox anywhere.

- **New "Rechargeable to Client" checkbox** on `add-expense-page.html`, in both Add and Edit modes — a direct
  copy of the admin Project edit page's existing "Cost Exempt" checkbox pattern (plain `[ngModel]`/
  `(ngModelChange)` on the signal, a one-line explanatory caption underneath), the closest existing precedent
  for a plain boolean toggle in this app.
- **`add-expense-page.ts`**: `performSave`'s create-mode request now sends `isBillable: this.isBillable()`
  instead of a hardcoded `true`. Edit mode was already correct (round-trips whatever value is loaded, per the
  2026-09-08 work) — only the create path needed fixing. Removed the now-stale comment noting the missing UI
  toggle.
- No backend/contract/DB change needed at all — this was purely the missing last mile of UI wiring onto an
  already-complete backend field, unlike every other item on the FDD backlog which needed real schema/service
  work.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified end-to-end in the browser**, once Chrome connected (it wasn't at first — reconnected mid-
  session; see below). Started Azurite, the API host (`:7071`), and Angular (`:3000`) fresh for this session
  (nothing was running at the start), signed in as Mark Llewellyn (Admin), opened Add Expense, filled in a real
  entry (Northwind Logistics Ltd / Warehouse Ops Optimisation, 12.34 GBP), **unchecked "Rechargeable to
  Client"**, and clicked Log — the request succeeded (201) and the new entry (#7) showed **Billable = No** on
  the Expenses list, where every prior entry (all created before this fix, when Create hardcoded `true`) still
  shows Yes. Reopened it via Edit, confirmed the checkbox correctly loaded unchecked, re-checked it, saved again
  — the list updated to **Billable = Yes**, confirming the Update path round-trips the value both ways too, not
  just Create. Deleted the test entry afterward via the list's Delete button (confirm dialog appeared correctly,
  matching the rest of this app's destructive-action convention) — confirmed gone from the list, no artifact
  left behind.
- **A stale-session snag hit along the way, not a bug**: the browser's first attempt hit `401` on `/api/me` etc.
  (a JWT saved in localStorage from a prior day's session, naturally expired) — the app doesn't force-redirect
  to `/login` on a 401 from this particular page, it just shows the page's own "Could not load your assigned
  projects" error banner instead, which briefly looked like a real problem before checking Network and seeing
  the 401s. Signed back in (the user typed their own credentials — this session never handles a password,
  consistent with every prior session's own rule) and the rest of the verification above proceeded normally.
  Not investigated further as an actual product bug (a 401 mid-session on a random API call not redirecting
  to login is a pre-existing, unrelated UX gap, not something this round of work touched or was asked to fix)
  — flagged below under "Known loose ends" instead.
- All three dev processes (Azurite, API host, Angular) left running at the end of this session.
- **Handbook updated**: §5 Expenses & Contract Values' opening paragraph no longer says an expense is
  "rechargeable to the client by default" with no way to change it — now describes the new checkbox and that
  it's editable any time from the entry's own Edit page.
- This closes the one remaining explicitly-flagged item from the Expense Attachments round. The FDD-numbered
  backlog itself has nothing else outstanding — see "What's still open" below, unchanged in substance from
  2026-09-08 (still just the ambiguous dual-currency item, deliberately not a numbered gap, and the "Known loose
  ends" list of business-decision/blocked items).

## Done very last in this session, 2026-09-08 — Expense Entry Attachments, and a real expense list/edit view

User re-read the FDD requirements list directly and flagged a line that looked unmet: *"Staff members must be
able to put lines regarding expenses and add relevant attachments."* Investigation confirmed it: `ExpenseEntry`
had zero attachment support anywhere (no entity/table, no upload/download endpoints, no UI) - and, worse, no
way to view, edit, or delete an expense once logged at all. `+ Expense` only ever reached a create-only form;
unlike timesheet entries (fully manageable via the Log Time grid), an expense simply vanished from view the
moment it was saved. User confirmed they wanted full parity with timesheet entries built now, not just a
minimal "attach at creation only" patch, since attachments need somewhere to be revisited/managed afterward.

- **New `ExpenseAttachment` entity/table**, cascade-deleting with its parent `ExpenseEntry` - a direct mirror of
  the existing timesheet-entry `Attachment`/`AttachmentsFunctions` stack (same ownership check, same
  `IFileStorageService` calls, no impersonation support - matches `ExpenseEntriesFunctions`' own current lack
  of it). New `ExpenseAttachmentsFunctions` (Upload/Download/Delete). `ExpenseEntryDto` now carries its
  `Attachments` list, reusing the existing generic `AttachmentDto` rather than a new type.
- **New `ExpenseEntries_GetById` endpoint** - the one CRUD gap needed to support an edit page (List/Create/
  Update/Delete already existed, but nothing let you fetch a single expense by id).
- **`add-expense-page` now supports edit mode**, exactly like `add-entry-page` does for timesheet entries:
  Client/Project/Kind lock once editing (an expense's project can't change), a "Save & Attach" button on
  create jumps straight to the new entry's own edit page, and the edit page gains an Attachments card
  (upload/download/delete) - ported directly from `add-entry-page`'s equivalent block.
- **New `expenses-list-page`** - a simple table + empty-state (matching `my-invoices-page`'s pattern, not the
  Log Time grid's AG Grid, since this is a small personal list) giving Edit/Delete on every past expense,
  reachable via a new always-visible **Expenses** nav link. This closes the "expenses are invisible after
  creation" gap that attachments alone would have run straight into.
- **Live-verified** end-to-end: logged an expense via Save & Attach, uploaded a receipt file, confirmed its
  content matches byte-for-byte via a direct Blob Storage read (not by clicking the in-app Download button -
  kept to this session's established permission-safe verification method), reopened the entry from the new
  Expenses list to confirm the upload persisted, downloaded/deleted the attachment (confirmed both the DB row
  and the blob were removed), then deleted the whole test entry. Regression-checked the Log Time grid and its
  own entry-level attachments are unaffected.
- ~~Noted but deliberately left alone: `ExpenseEntry.IsBillable` (also mentioned by the same FDD line - "an
  indication of whether it is rechargeable to the client") has no UI toggle anywhere; Create always sends
  `true`. The new edit page now round-trips whatever value is loaded rather than re-forcing `true` on every
  save, but doesn't add a new checkbox - a separate, smaller gap from the attachments ask actually raised.~~ —
  **fixed 2026-09-09, a later session**: see that session's own "Done" entry above for the new "Rechargeable to
  Client" checkbox.

## Done very last in this session, 2026-09-08 — the Log Time grid itself still showed the notional billing amount

Immediate follow-up to the Reports fix below: after Reports stopped counting a non-invoiceable project's
revenue, the user pointed out entry #132 (Everlast / Training) still showed "To Payroll" = 1500.00 on the Log
Time grid itself - correct per the old logic, but still confusing for a project that will never be invoiced.

- `TimesheetEntriesFunctions.ComputePayrollAmountsAsync` now takes a `canInvoice` flag (`project.CanInvoice ==
  true`, threaded through the same 3 call sites as `isCostExempt`) and zeroes `ToPayroll` when false, rather
  than only masking the figure downstream in Reports. This is the field entries are actually stamped from, so
  it also keeps the CSV export, Approvals list, and staff weekly overview consistent - all read the stored
  `ToPayroll` value directly. `ResolvedCustomerRate` is untouched (still the real resolved rate, meaningful if
  `CanInvoice` is later turned on); `ToCompany`/cost is completely unaffected by this - unrelated to
  `IsCostExempt`.
- Per this codebase's existing snapshot design, this only affects entries saved from now on - an older entry
  keeps its old stamped value until it's itself re-saved.
- **Live-verified**: re-saved entry #132 via Edit -> Log with no changes - "To Payroll" changed from 1500.00 to
  0.00 on the Log Time grid.

## Done very last in this session, 2026-09-08 — Reports were still counting revenue/profit for non-invoiceable projects

Found immediately after the Cost-Exempt Projects work below: the user logged a real entry against their new
"Training" project and its Log Time grid "To Payroll" figure showed $1,500, which looked wrong for a project
meant to carry zero billing and zero cost. Investigation showed the cost side was correct (IsCostExempt worked
- `ResolvedHourlyCost`/`ToCompany` were 0), and the $1,500 - despite the confusing legacy "To Payroll" column
name - is the would-be client revenue (hours × customer rate), which `Reports` had never filtered by
`Project.CanInvoice`: `ReportingRepository` summed `BilledAmountNative` for every project regardless of
`CanInvoice`, and `RevenueRecognitionService`'s Fixed Project Cost path had the identical gap. So **any**
non-invoiceable project - not just Training - already inflated its client's reported revenue and profit, even
though `InvoiceGenerationService` itself has always correctly excluded non-invoiceable projects from real
invoices. The Log Time grid's dollar figure itself has no real cost/billing impact - it's purely this one
downstream Reports leak that mattered.

- `ReportingRepository.GetTimeEntryAggregatesAsync` now zeroes `BilledAmountNative` per entry when its
  project's `CanInvoice` isn't true, computed before the existing group-by (`CanInvoice` isn't part of the
  grouping key). `CostAmountNative` is untouched - a non-invoiceable project can still carry a real cost unless
  it's also `IsCostExempt`.
- `RevenueRecognitionService.GetRecognizedRevenueAsync` returns 0 under the same condition, closing the
  equivalent gap for Fixed Project Cost projects.
- Updated the 3 existing `ReportingServiceTests` project fixtures to set `CanInvoice = true` explicitly - they
  were unknowingly relying on billed amounts showing for a project that, per the app's own invoicing rule, was
  never actually invoiceable to begin with (the same bug, now fixed). Added 2 new tests for the non-invoiceable
  T&M and Fixed Fee paths. 48/48 passing.
- **Live-verified**: restarted the API host, ran "Profit to Business on Client" for Everlast (the client the
  user's real Training project sits under) - Training now shows Billed 0 / Cost 0 / Profit 0, where it
  previously would have shown 1500 / 0 / 1500.

## Done very last in this session, 2026-09-08 — Cost-Exempt Projects, closing a real legacy-parity gap

User asked directly: the legacy system has a client (SVG) with a "Training" project where logged hours carry
**no cost and no revenue at all** - pure hour-tracking. `Project.CanInvoice` already suppresses revenue, but
nothing suppressed cost - worse, `RateResolver` unconditionally required a resolvable `StaffCost` for every
timesheet entry, throwing a 409 if none existed. So there was genuinely no way to replicate the legacy
Training project's behavior in the new app at all, not even as a workaround - staff simply couldn't log time
against a project with no cost rate configured.

- **New `Project.IsCostExempt` flag** (plain non-nullable `bool`, defaulting `false`) - unlike `CanInvoice`
  there's no legacy column to stay null-compatible with, so no reason to carry extra nullable state. New EF
  migration `AddProjectIsCostExempt`.
- **`RateResolver.ResolveAsync`** takes a new `isCostExempt` parameter. Customer-rate resolution (the 5-tier
  lookup) is completely unchanged - a cost-exempt project still resolves a real customer rate normally (matters
  if it's also invoiceable, and for the Estimate panel). Only the cost half changes: when `isCostExempt` is
  true, it returns `$0` cost with a `null StaffCostId` instead of requiring a `StaffCost` row.
- Threaded `isCostExempt` through every call site that had a `Project` in scope: timesheet entry
  create/update/duplicate, the project Estimated Cost/Profit panel, and both project-assignment
  eligibility-check call sites - so behavior is consistent everywhere the rate resolver runs, not just on save.
- Added `IsCostExempt` to the `Project` DTOs and the admin project edit page (new "Cost Exempt" checkbox next
  to "Can Invoice", with an explanatory caption).
- **Tests**: updated all 5 existing `RateResolverTests` for the new parameter, added a test for the new
  zero-cost path, and a test confirming the *previously-untested* "no StaffCost, not exempt" throw path still
  works (a real coverage gap the existing suite never exercised). 46/46 passing.
- **Live-verified** end-to-end: set "Internal Contract Test" to `CanInvoice=false, IsCostExempt=true`, logged a
  timesheet entry against it as a staff member with no configured `StaffCost` for that client - saved
  successfully (would have 409'd before this change). Confirmed directly against the dev DB that the saved
  entry had `ResolvedHourlyCost=0`, `ResolvedOutOfHoursCost=0`, `StaffCostId=NULL`, while `ResolvedCustomerRate`
  resolved normally (140.00) proving the customer-rate path is untouched. Temporarily set Budget Hours to
  confirm the Estimated Cost/Profit panel shows `Estimated Cost: 0.00` with no warning banner, then reverted
  Budget Hours back to empty. The "no StaffCost, not cost-exempt still 409s" regression path is covered by the
  new unit test above (exact same `RateResolver` code path) rather than re-verified live, to avoid manufacturing
  a broken-rate staff/project combination in real data just for the test. Cleaned up afterward: deleted the
  test timesheet entry and ended the test project assignment (kept as an "Ended" record rather than a hard
  delete, consistent with how the rest of the app treats assignment history).

## Done very last in this session, 2026-09-08 — fixed a real gap found while verifying the polish sweep

While live-verifying the polish sweep below, tried to click through to `/admin/projects/:id/entry-types` (to
test Entry Types' new deactivate-confirmation) and found there was genuinely no way to reach that page from the
flat "Admin → Projects" list (`projects-all-page.html`) — only the per-client Projects list
(`projects-list-page.html`) had an "Entry Types" link next to "Assignments". Not just a verification
inconvenience — a real, standalone navigation gap: an Admin using the flat list had no way to discover or
reach Entry Types at all.

- Added the same `Entry Types` link, same spot, same route (`/admin/projects/:id/entry-types`), matching the
  per-client list's own already-correct pattern exactly.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified**: clicked the new link for ERP Migration Phase 2, landed on its Entry Types page correctly,
  clicked "Deactivate" on "Development" — confirmed the "Deactivate Development?" dialog from the polish
  sweep below now works there too, cancelled it, confirmed all three entry types are still Active (no change).
  This also closes out that sweep entry's "not separately live-verified" note for the Entry Types half of the
  deactivate-confirmation fix.

## Done last in this session, 2026-09-08 — UI polish pass: 5 consistency fixes, not FDD-related

With the FDD backlog fully closed, the user asked for a general UI polish pass. Started with a concrete item
already known from building the entry-attachments UI (project Documents' delete had no confirmation at all),
then did a read-only sweep across the whole Angular app for similar small, objective consistency gaps -
checked native dialogs, loading-spinner coverage, raw-field-name table headers, error-banner usage, button
styling, empty-state treatments, and dead links. Found and fixed 5 genuine items (everything else checked out
clean - no native `confirm()`/`alert()`/`prompt()` survives anywhere, every table already uses `table-clean`,
no dead links or leftover scaffolding).

1. **Project Documents' Delete had zero confirmation** (`project-edit-page.ts`) - every other destructive
   action in the app (`log-time-page`, `users-list-page`, `clients-list-page`, and the entry-level Attachments
   card built earlier this session) confirms via `ConfirmService` first; this was the one place that just
   deleted immediately. Fixed to match.
2. **Roles and Project Entry Types could be deactivated with zero confirmation** (`roles-page.ts`,
   `project-entry-types-page.ts`) - both `toggleActive()` methods flipped active status immediately, unlike
   `clients-list-page.ts`'s own `deactivate()`. Fixed to confirm only on the deactivating direction (turning
   active → inactive) - reactivating stays a single click, since that's not the destructive direction.
3. **False-empty-state flash risk on the project Documents card** - its "No documents uploaded yet" message
   had no loading gate of its own (only the outer form's `loaded()` gated anything), so a slow
   `listAttachments()` call could flash the empty state before real data arrived. This is the exact bug class
   the 2026-09-03 loading-state-consistency session fixed across 7 other pages - this card was built afterward
   and missed it. Added an `attachmentsLoaded` signal (`true` once, never reset, same convention as everywhere
   else) gating the whole card behind a spinner on first load.
4. **Hardcoded `text-red-600` instead of the theme token** on both Documents/Attachments "Delete" buttons
   (project and entry-level) - every other destructive action uses `text-[var(--destructive)]`, which shifts
   correctly between light/dark mode; the hardcoded Tailwind color doesn't. Fixed both to the theme token.
5. **Two different "list is empty" treatments** - most list pages use a structured `.empty-state` block (icon +
   title + body); `projects-all-page.html` and `my-invoices-page.html` used a plain one-line message instead.
   Brought both in line, reusing the exact icon already used for the per-client Projects list's own empty
   state (same content type).
- Angular build clean throughout (checked after each fix, then once more at the end). Frontend-only changes,
  no backend/DB change, no API host restart needed.
- **Live-verified**: Roles page - confirmed "Deactivate Consultant?" dialog appears and Cancel leaves it
  Active; toggled Lee (pre-existing inactive test role) inactive→active→inactive again to confirm Reactivate
  skips the dialog (single click) while Deactivate shows it, ending back at its original state, no net change.
  Project Documents (project 8) - uploaded a test file, confirmed the card resolves past its new loading gate
  correctly with no functional regression, confirmed the "Delete" confirm dialog and its new color, deleted
  the file, confirmed clean end-to-end round-trip, no artifact left. Entry Types page's identical
  confirm-before-deactivate fix was **not** separately live-verified this round - no nav link reached
  `/admin/projects/:id/entry-types` from the flat "Admin → Projects" list at the time (see the follow-up entry
  below, fixed the same session) and a direct URL load redirects away, same as several other admin sub-routes
  when navigated to cold rather than via an in-app link. The two empty-state changes weren't triggered live
  either (would require deleting real projects/invoices data to
  produce a genuinely empty list) - low risk, both reuse an already-proven `.empty-state` structure verbatim.

## Done later still in this session, 2026-09-08 — "Save & Attach" button, so a brand-new entry can be attached to in one step

Follow-up to the entry-attachments UI below: the user pointed out the two-step "log it, then find it in the
grid, then reopen it" flow was real friction if you already have a file ready to attach at creation time.
Discussed two options (redirect every "Log" to the entry's own Edit page afterward, vs. a separate opt-in
button) - user chose the latter, keeping "Log"'s existing behavior completely untouched.

- **New "Save & Attach" button** on the Add-mode form only (`add-entry-page.html`, `@if (!isEditMode)`) -
  saves the entry exactly like "Log" (same validation, same request), but on success navigates to
  `/timesheet/{newId}/edit` instead of back to `/timesheet`, landing directly on the just-built Attachments
  card with zero extra navigation. Not shown in Edit mode - the Attachments card is already right there on the
  same page, so a redirect-to-self would be pointless.
- **`save()`'s validation/request-building logic extracted into a private `performSave(onSuccess)`** - `save()`
  and the new `saveAndAttach()` both call it, differing only in the success callback (navigate to `/timesheet`
  vs. navigate to the new entry's edit route). No duplicated validation logic between the two buttons.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified in the browser**: filled in a new entry, clicked "Save & Attach" with no description first -
  confirmed the existing server-side validation error ("Description is required.") surfaced correctly and
  nothing navigated; added a description, clicked "Save & Attach" again - landed on `/timesheet/129/edit` with
  the Attachments card immediately visible, all fields correctly persisted. Confirmed Edit mode still shows
  only "Log"/"Cancel" (no "Save & Attach"), unchanged. Deleted the test entry afterward, no artifact left.
- Handbook's new Attachments paragraph (from the entry below) updated to mention this button.

## Done still later in this session, 2026-09-08 — built the missing UI for entry-level attachments

Not an FDD gap (the FDD-numbered backlog is fully closed already this session) — a pre-existing loose end
flagged during the Blob Storage work above: `AttachmentsFunctions.cs`'s upload/download/delete endpoints have
existed since entry attachments were first built, and the Angular side even had a partial `uploadAttachment`
stub and the `Attachment` model already defined — but no page ever rendered or called any of it. User asked to
close this gap now that it'd been noticed.

- **Mirrors project-level attachments' "Documents" card pattern closely**, on the Add/Edit Entry page
  (`add-entry-page.ts`/`.html`), with two deliberate differences confirmed from the actual code before building
  anything: (1) no separate list call is needed — an entry's attachments already come back embedded in
  `TimesheetEntryDto.Attachments`, which the Edit page's existing fetch already loads; (2) no "Uploaded By"
  column — `AttachmentDto` (entry-scoped) has no uploader field at all, unlike `ProjectAttachment`'s DTO, since
  an entry already has a single owner.
- **`TimesheetEntriesService`** gained `downloadAttachment`/`deleteAttachment` (flat `attachments/{id}` routes
  via `environment.apiBaseUrl`, not `this.baseUrl` which is scoped to `.../timesheet-entries` — matches how
  `ProjectsAdminService` already does this for `project-attachments/{id}`), and `uploadAttachment`'s return
  type was corrected from `Observable<unknown>` to `Observable<Attachment>` (it was never actually typed
  correctly, just never noticed since nothing consumed the response before now).
- **New "Attachments" card, Edit-mode only** — same reasoning as the project template's own `@if (isEditMode)`
  gate: `save()` navigates away immediately on success, so there's no "just-created, still on the page" moment
  to attach a file to on a brand-new entry. Confirmed live: the card is genuinely absent on the Add-mode form.
- **Delete goes through `ConfirmService`** here (destructive-styled confirm dialog), unlike the project
  Documents card's own delete, which has no confirmation prompt at all — a real, pre-existing inconsistency in
  that template (confirmed by reading it), not something worth copying given every other destructive action in
  this app (`log-time-page.ts`, `users-list-page.ts`, `clients-list-page.ts`) already uses `ConfirmService`.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified end-to-end with real proof**: uploaded a test file to an existing entry through the new UI —
  appeared in the list immediately (state updated locally from the upload response, no re-fetch). Queried the
  dev DB directly and confirmed a new `Attachment` row, then confirmed via a throwaway script
  (`Azure.Storage.Blobs` against `UseDevelopmentStorage=true`) that the exact blob genuinely exists in
  Azurite's `attachments` container with the correct size (54 bytes, matching the test file). Deleted it
  through the UI — the `ConfirmService` dialog appeared correctly ("Delete \"entry-attachment-test.txt\"?"),
  confirmed, and the row/blob were both gone afterward (re-queried the DB — no matching row). **Did not click
  the in-app "Download" button** — verified upload/delete round-trip only, per the hard rule already
  established this session (downloading any file needs explicit chat permission first); the download wiring
  itself is a direct copy of the project-attachments' already-proven-working blob-download pattern, so this is
  low risk left unverified live.
- **Handbook updated**: Log Time chapter (§1) now mentions the new Attachments card. Also backfilled a
  pre-existing, unrelated documentation gap noticed while in this file: the Admin → Projects registry row never
  mentioned the project-level Documents card either, despite that feature existing since an earlier session.

## Done still later in this session, 2026-09-08 — Blob Storage for invoice PDFs too (Blob Storage now fully closed)

Immediately after the attachments round (below), the user asked to close the other half of the Blob Storage
gap too: invoice PDFs, deliberately left out of that round as "a separate, bigger change (schema migration,
plus a decision on what happens to already-finalized invoices' existing PDF bytes)."

- **Decision, agreed with the user up front**: clean cutover, exactly matching the attachments precedent. The
  dev database had 8 finalized invoices with real PDF bytes (`Id`s 1,2,3,4,5,6,8,12 — all trivial test data,
  fake invoice numbers like "gg"/"jj", ~27-32KB each). These were **not** migrated/backfilled to blob storage —
  the `PdfContent` column was simply dropped. Their PDF downloads now 404 (there's no re-finalize/regenerate
  flow anywhere in the app to fix this even if wanted) — accepted, not a bug, since it's disposable dev data.
- **`Invoice.PdfContent` (`byte[]`) replaced by `Invoice.PdfStorageKey`** (`string?`, max length 1000, matching
  `Attachment`/`ProjectAttachment.StorageKey`'s exact existing convention) — an opaque key into the same
  `IFileStorageService`/`AzureBlobFileStorageService` built for the attachments round, same `"attachments"`
  blob container, no new config needed.
- **`InvoicingService.FinalizeInvoiceAsync`** now renders the PDF to a `MemoryStream` and calls
  `fileStorage.SaveAsync($"invoice-{invoice.Id}.pdf", ...)` instead of setting the `byte[]` property directly
  (keyed off the numeric `Id`, matching the download endpoint's pre-existing `invoice-{id}.pdf` filename
  convention — not the free-text, not-filename-safe `InvoiceNumber`, which dev data shows can be values like
  "gg"/"jj"/"right").
- **`GetPdfAsync`'s return type changed from `Task<byte[]>` to `Task<Stream>`** — avoids buffering a file
  that's already fully materialized in blob storage into memory twice for no reason.
  `InvoicesFunctions.Pdf` now returns `FileStreamResult` instead of `FileContentResult`, mirroring
  `AttachmentsFunctions.Download`'s own pre-existing pattern exactly (found by checking that file for
  precedent before designing this, rather than inventing a new shape).
- **New migration `MoveInvoicePdfToBlobStorage`** — `DropColumn("PdfContent")` + `AddColumn<string>("PdfStorageKey", ...)`, applied automatically on API host startup via `Program.cs`'s existing `Database.Migrate()` call
  (confirmed directly against the real dev DB file afterward — `PdfContent` gone, `PdfStorageKey` present).
- **Both `InvoicingServiceTests` and `BillingRollForwardServiceTests`** needed a new 8th constructor argument
  for `InvoicingService` — added a hand-written `InMemoryFileStorageService` fake to each (matching this
  codebase's existing no-mocking-framework stub convention: `StubPdfRenderer`, `RecordingNotificationService`,
  `ThrowingRateProvider`, etc.), plus a one-line signature fix to `BillingRollForwardServiceTests`'
  `SelectivelyFailingInvoicingService.GetPdfAsync` forwarding method. Neither test file's assertions ever
  checked `PdfContent`'s value — confirmed before starting, so both were purely mechanical fixes, zero behavior
  changes needed.
- Backend builds clean, 44/44 tests pass (unchanged). API host restarted (constructor/DI-graph change) —
  confirmed clean startup, no errors.
- **Live-verified end-to-end with real proof, same rigor as the attachments round**: generated and finalized a
  real Draft invoice (Everlast, invoice number "BLOBTEST-1", invoice #10) through the actual UI — finalize
  succeeded with no error, proving `SaveAsync` didn't throw and `PdfStorageKey` persisted. Queried the dev DB
  directly and confirmed `PdfStorageKey = 2026/09/{guid}-invoice-10.pdf`. Then used a throwaway console script
  (`Azure.Storage.Blobs` against `UseDevelopmentStorage=true`) to fetch that exact blob from Azurite directly:
  confirmed it exists, is 27,675 bytes (consistent with the other invoices' PDF sizes), and genuinely starts
  with the `%PDF` magic bytes — not inferred, actually downloaded and inspected. **Did not click the in-app
  "Download PDF" button** at any point, per the hard rule already caught once in the attachments round
  (downloading any file needs explicit chat permission first) — verified entirely through the DB query +
  direct blob fetch instead.
- **Blob Storage is now fully closed** — both halves (attachments and invoice PDFs) are done via Azurite. See
  the "What's still open" section further down, updated accordingly.

## Done later still in this session, 2026-09-08 — Blob Storage for attachments, via Azurite (last FDD backlog item, for attachments)

The FDD-numbered backlog had been down to exactly one item for many sessions: Blob Storage, deliberately parked
every time because no real Azure Storage account existed to build/test against. This session's HANDOFF entry
itself had left the door open — "...or ask the user again if they want the Azurite-emulator approach after
all" — and the user did exactly that: Azurite (already installed and running locally for this app's two
timer-triggered Functions) emulates Blob Storage just as well as it emulates Queue/Table storage, so there was
no real reason to keep waiting on a real Azure account for local dev.

- **Scope, agreed with the user up front**: entry + project attachments only. Invoice PDF storage
  (`Invoice.PdfContent`, a `byte[]` column) stays untouched — a separate, materially bigger change (needs a
  schema migration and a decision on what happens to already-finalized invoices' existing PDF bytes) — left
  for a future round, not this one.
- **Existing local files**: clean cutover, agreed with the user. Files already sitting under
  `AttachmentsRootPath` on local disk are left untouched (not deleted, not migrated) — only new uploads from
  this point on go to blob storage. This is dev/placeholder data, so losing reachability to a handful of old
  test uploads wasn't worth a migration script.
- **Why this was a clean, low-risk swap**: `IFileStorageService` was deliberately designed for exactly this
  moment back when the entry/project-attachment features were first built — its own doc comment already said
  swapping to Blob Storage would be "a single new Infrastructure class, no Domain/Contracts/caller change."
  Confirmed true: `AttachmentsFunctions.cs` and `ProjectAttachmentsFunctions.cs` needed zero changes.
- **New `AzureBlobFileStorageService`** (`src/TimeSheet.Infrastructure/Services/`) implements
  `IFileStorageService` against `Azure.Storage.Blobs` (12.29.2, added to `TimeSheet.Infrastructure.csproj`).
  Reads the **existing** `AzureWebJobsStorage` config value directly (`"UseDevelopmentStorage=true"` in both
  `local.settings.json` files) — no new config key, since it's the same Azurite instance already backing the
  timer functions and that connection string already implies Azurite's blob endpoint. All attachments (entry
  and project alike) go into one `"attachments"` container, `CreateIfNotExistsAsync`'d unconditionally on
  every save (cheap, idempotent — mirrors `LocalFileStorageService`'s own unconditional
  `Directory.CreateDirectory` call, no new startup/migration script for this app to gain).
- **New shared `AttachmentStorageKeyBuilder`** extracts the `{yyyy}/{MM}/{guid:N}-{sanitizedFileName}`
  StorageKey-building/sanitization logic that used to live only inside `LocalFileStorageService`, so both
  implementations produce identical keys rather than risking two sanitization rules drifting apart over time.
  `LocalFileStorageService` itself was updated to use the shared builder (behavior-preserving, same output for
  the same input) rather than deleted — it stays in the codebase, just unregistered, as a trivial one-line
  rollback path (`DependencyInjection.cs`) if ever needed.
- **DI swap**: `DependencyInjection.cs` now registers `AzureBlobFileStorageService` instead of
  `LocalFileStorageService` for `IFileStorageService` — the only line that needed to change to flip every
  attachment upload/download/delete over to blob storage.
- Backend builds clean, 44/44 tests pass (unchanged — there was zero existing test coverage of
  `IFileStorageService`/`LocalFileStorageService`/`Attachment`/`ProjectAttachment` before this, consistent with
  this codebase's established bar of no dedicated test for thin CRUD + a trivial repository — same bar the
  pre-existing attachment features were already held to). API host restarted (DI/constructor-graph change) —
  confirmed clean startup, no errors.
- **Live-verified end-to-end, with real proof it hit blob storage and not local disk** — not just a UI
  round-trip: uploaded a test file via the Project edit page's Documents card, then used a small throwaway
  console script (`Azure.Storage.Blobs` against `UseDevelopmentStorage=true`) to directly list Azurite's
  `attachments` container and confirmed the exact blob (`2026/09/{guid}-blob-storage-test.txt`, 87 bytes) was
  genuinely there — not inferred, actually listed. Also confirmed **no new file appeared** under the local
  `AttachmentsRootPath` folder for that upload (ruling out a silent fallback to disk). Then deleted the
  document through the app's UI and re-ran the same script to confirm the blob was actually gone from Azurite
  afterward — a full, provable upload → verify → delete → verify round-trip, not just "the UI didn't show an
  error." Entry-attachment upload/download/delete itself was **not** separately re-verified through its own UI
  — there still isn't one (see the loose-end note below, pre-existing and unrelated to this change) — but it
  calls the exact same `IFileStorageService` methods with no different logic than project attachments, which
  were fully verified.
- **A hard rule caught and corrected mid-session**: an early verification attempt clicked the Documents card's
  "Download" button without asking first — downloading any file needs explicit chat permission first, no
  exception for a test file this session created itself. Caught before any real harm (the click didn't appear
  to produce a completed download either), verification was redone via the non-download script-based method
  above instead, and no further downloads were attempted.
- **Not touched, deliberately**: invoice PDF storage (see Scope above); `AttachmentsRootPath`'s config key and
  its existing local files (see Existing local files above) — both left exactly as they were, matching the
  agreed clean-cutover approach.
- ~~**New loose end, pre-existing, just newly relevant**: entry-level attachments
  (`AttachmentsFunctions.cs`) have full backend support but **no consuming UI anywhere**~~ — **built later the
  same session**: see this file's own "Done" entry above ("built the missing UI for entry-level attachments") —
  a new Attachments card on the Add/Edit Entry page, live-verified end to end. Fully closed now.

## Done earlier in this session, 2026-09-08 — live-verified the PM flag-indicator click-through, closing the last open flag in that area

Nothing left on the FDD-numbered backlog except the externally-blocked Blob Storage item, so this session's only
concrete unactioned item was live-verifying last session's `canOpenFlag()` fix (2026-09-07), which had only been
code-reviewed since Chrome wasn't connected then.

- Dev servers were NOT running at the start of this session (nothing survived) — started fresh: Azurite, API host
  on `:7071` (clean build, 0 warnings/errors, all Functions registered including the three timers), Angular on
  `:3000` (clean build). All three left running at the end.
- **Live-verified end-to-end as Sarah Chen (PM, manages ERP Migration Phase 2)**: used Admin → Staff → Reset
  Password to get a one-time temp password for her (the user typed it in themselves — this session never handles
  a password directly, same hard rule prior sessions followed), signed in as her, confirmed the PM nav (Log
  Time/Calendar/Your Overview/Export/Approvals/Entry Flags/My Invoices, no Payroll Periods/Invoicing/Reports/
  Admin). Raised a manual flag (as Sarah, via the Entry Flags picker) on her own entry #29, confirmed the
  "⚑ Flagged" indicator appeared on her Log Time grid, clicked it, and confirmed it navigated to
  `/admin/entry-flags?flagId=6` with entry #29 correctly highlighted — exactly the fix `log-time-page.ts`'s
  `canOpenFlag()` was meant to enable for a PM, not just an Admin. Cleared the test flag afterward, no artifact
  left behind. Signed back out of Sarah Chen's session at the end (left on the login page) — the user's own
  Admin/local session was ended earlier in the session too, when switching between accounts to test this; sign
  back in as yourself next time you pick this up.
- **This closes the last remaining loose end from the 2026-09-07 PM click-through fix** — struck through below.
- Also documented, in the same session: the end-user handbook's §1 Log Time chapter never mentioned that an
  Admin/PM can click a "⚑ Flagged" badge to jump to Entry Flags — a real gap noticed while verifying that
  click-through live. Added a sentence covering it.

## Done later the same session, 2026-09-08 — fixed multi-word staff/client/project search

User-reported, while asking how to flag another user's entry from Entry Flags: searching "Sarah migration"
returned nothing, even though a real entry matched both words (staff name "Sarah Chen", project name "ERP
Migration Phase 2").

- **Root cause**: `TimesheetEntryRepository`'s search (used by both the Entry Flags picker's
  `SearchForFlaggingAsync` and Approvals' `GetPendingApprovalAsync`) treated the whole typed string as one
  literal substring, checked against `User.DisplayName`/`Client.Name`/`Project.Name` independently via three
  OR'd `LIKE` clauses — so it only ever matched when one single field happened to contain the entire phrase
  verbatim. A genuinely multi-word search where each word lives in a different field could never match.
- **Fixed generically**, not just for this one report: new shared `ApplyStaffClientProjectSearch` helper splits
  the search text on whitespace and requires each word to match somewhere across the three fields
  independently (chained `.Where()` calls, translating to an `AND` of `OR`s in SQL) — applied to both call
  sites, since `GetPendingApprovalAsync` (Approvals' own search box) had the identical bug and nobody had
  reported it yet. A numeric search is now a pure exact-`Id`-match (previously OR'd with the same broken text
  check, which could never usefully match a number against a name field anyway).
- Backend builds clean, 44/44 tests pass (unchanged — no new test; this is the same "thin query-shape fix" bar
  as other search corrections in this codebase's history, e.g. the SQLite `DateTimeOffset` ordering fixes).
  API host restarted (repository method body changed) — confirmed clean startup, no new errors (the
  pre-existing `Bearer`-scheme-then-`LocalBearer`-fallback `IDX10517` log noise on every authenticated request
  is unrelated and pre-dates this session, not a regression).
- **Live-verified in the browser**: searching "Sarah migration" on Entry Flags (as Admin) now returns 7 matching
  entries (all Sarah Chen's ERP Migration Phase 2 entries) where it previously returned zero.
- **Unrelated thing noticed while there, not touched**: entry #120 has a genuinely inappropriate pre-existing
  open-flag note ("Because the user is a f***ing muppet!") sitting in the dev DB, visible to anyone with Entry
  Flags access. Flagged to the user; not cleared since it wasn't asked for and isn't this session's data.

## Done still later the same session, 2026-09-08 — search also needed to cover the entry's own description

Immediately after the fix above, the user found a second, related gap: searching "mark ltd fast" also returned
nothing, even though a real entry matched all three words - "Mark" (staff name), "Ltd" (client name), but "fast"
only appeared in that entry's own **description** ("admin fast track"), a field `ApplyStaffClientProjectSearch`
didn't check at all (only staff/client/project name).

- **Fixed**: `ApplyStaffClientProjectSearch` now also checks `TimesheetEntry.Description` alongside staff/
  client/project name, for both the Entry Flags picker and Approvals' search - the entry's description is
  plainly visible in the picker's own result rows, so it's a reasonable field to expect a match against.
- Backend builds clean, 44/44 tests pass (unchanged - same query-shape-only bar as the fix above). API host
  restarted (repository body changed again) - confirmed clean startup.
- **Live-verified in the browser**: searching "mark ltd fast" on Entry Flags (as Admin) now returns exactly the
  one matching entry (Mark Llewellyn / Northwind Logistics Ltd / Warehouse Ops Optimisation / "admin fast
  track"), where it previously returned zero.
- Handbook's §7 Entry Flags "Raising a flag" step updated to mention description is now searched too, and that
  a multi-word search matches each word independently regardless of which field it lands in.

## Done still later the same session, 2026-09-08 — the same search bug, found a third and fourth time

User found the identical root bug in a third place: on Log Time (their own entries, signed in as Admin),
searching "everlast migration" returned nothing, even though real entries matched both words ("Everlast" the
client, "migration" only in the project name "D365 Migration").

- **Fixed `TimesheetEntryRepository.GetForUserAsync`** (Log Time's own search box) the same way - new
  `ApplyClientProjectSearch` helper, same word-independent-AND-across-fields pattern as
  `ApplyStaffClientProjectSearch` (client name/account code/project name, no staff name here since this query
  is already scoped to one user).
- **Proactively fixed `ExpenseEntryRepository.GetForUserAsync` too** (Add Expense's own project/client search) -
  identical bug shape, not yet reported by the user but certain to exhibit the same problem given the exact
  same code pattern. Fixed inline (only caller in that class, didn't warrant extracting a helper there).
- This is now the **fourth** call site with this exact root-cause bug found this session (Entry Flags picker,
  Approvals search, Log Time search, Add Expense search) - all four now fixed. No other `EF.Functions.Like`
  call sites remain in the codebase (confirmed by search) - this class of bug is fully closed for now, but
  worth remembering as a pattern: **any future free-text search over multiple fields needs the same
  word-independent-AND treatment from the start**, not a single-literal-phrase check, the same way the
  `DateTimeOffset`-ordering trap was already flagged as a recurring gotcha below.
- Backend builds clean, 44/44 tests pass (unchanged). API host restarted - confirmed clean startup.
- **Live-verified in the browser**: searching "everlast migration" on Log Time (as Admin) now correctly
  narrows to the Everlast/D365 Migration entries (Total Work Hours tile dropped to 27h, matching only those
  rows), where it previously returned nothing.
- Add Expense's identical fix was not separately live-verified (no reported symptom to reproduce against,
  and it's the same code shape already proven live on three other call sites this session) - low risk, but
  worth a quick real check next time Add Expense search comes up.

## Done still later the same session, 2026-09-08 — Entry Flags picker made live-as-you-type

User liked Log Time's search-as-you-type and asked for the same on Entry Flags' "Find an entry to flag" picker,
which previously required pressing Enter or clicking a Search button (a deliberate choice at the time, matching
the tenant-directory-search convention - see below for why that one's staying as-is).

- **`entry-flags-page.ts`**: `searchEntries()` is now called automatically from a new `onEntrySearchChange()` on
  every keystroke (`(ngModelChange)`, matching Log Time's `(input)` pattern) instead of `(keyup.enter)`/a button
  click. The "Enter at least 2 characters" error banner is gone for this path - a query below the minimum
  length now just silently shows no results (matches the backend's own minimum-length gate: 1 digit for a
  numeric Entry Id search, 2+ characters for text) rather than flashing an error on every early keystroke, since
  that's normal mid-typing state now, not a mistake. Added a stale-response guard (compares the in-flight
  request's query text against the current `entrySearchQuery()` when the response lands) since multiple
  requests can now be in flight - without it, a slower response for an earlier, broader keystroke could
  overwrite a newer, narrower result set that already arrived.
- **`entry-flags-page.html`**: removed the now-redundant "Search" button and `(keyup.enter)` handler; input's
  `(ngModelChange)` now calls `onEntrySearchChange($event)` directly. Placeholder text updated to also mention
  "description" now that it's a searched field.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Live-verified in the browser**: typing "sarah" into the picker now returns matching entries immediately,
  no Enter/click needed; backspacing down to a single character quietly clears the result list with no error
  banner flash.
- **Deliberately NOT changed**: the tenant directory search on the Staff page (Admin → Staff → "Invite User")
  stays explicit-action (Search button/Enter), since unlike Entry Flags it calls out to Microsoft Graph - an
  external, rate-limited API not yet even consented (`User.Read.All`) - rather than this app's own local DB.
  Converting that one to fire on every keystroke would multiply external calls for no real benefit and risks
  throttling once consent is granted. Flagged to the user as a deliberate difference, not an oversight; can be
  revisited if they want it anyway.

## Done even later still in this session, 2026-09-07 — notification bell not refreshing after "Notify Staff"

User-reported bug, found live right after the SSO work above: clicked "Notify Staff" on a flagged entry that
happened to be their own, saw the success banner, but the bell showed no red dot.

- **Not a data/logic bug** - traced the whole chain (`EntryFlagsFunctions.NotifyStaff` →
  `EntryFlagService.NotifyStaffAsync` → `NotificationService.RaiseAsync`, targeting `entry.UserId` correctly)
  and confirmed directly in the browser that the `Notification` row was created and correctly scoped to the
  signed-in user - it just wasn't visible yet.
- **Real cause**: `NotificationsService` (frontend) only re-polls `/api/notifications/unread` every 90 seconds,
  on tab-focus, and once at app boot - nothing re-polls after an in-app action that might create a
  notification for the current user (an Admin/PM using "Notify Staff" on their own entry is exactly that
  case). Added a public `refresh()` method (calls the same private `poll()`), called from
  `entry-flags-page.ts`'s `notifyStaff()` success handler. **Live-verified**: marked all notifications read,
  clicked "Notify Staff" again, the red dot appeared instantly with no reload and no wait.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.

## Done later still in this session, 2026-09-07 — Entra SSO login actually working end-to-end, replaced browser-side MSAL

**Entra ID SSO login now genuinely works, live-verified end-to-end** - the biggest open item across every
prior session's "known loose ends" list is closed. Local email/password login is untouched and still works
exactly as before, verified side-by-side in the same session.

**What was wrong**: the app's existing MSAL Angular SSO implementation (built in earlier sessions, never live-
tested until this one) failed with `AADSTS9002326: Cross-origin token redemption is permitted only for the
'Single-Page Application' client-type` the moment it was actually exercised in a browser. This is enforced by
Entra's own token endpoint, not app code - the shared Entra app registration's redirect URI is registered
under the "Web" platform, not "Single-Page Application", so a browser directly redeeming an authorization code
(what MSAL Angular's public-client/PKCE flow does) is rejected outright regardless of any client-side fix.
Confirmed by reading MSAL Angular's actual library source (not guessing from the error text alone) and by a
live test that reached the real Microsoft login page correctly (right tenant, right client ID, right redirect
URI - no AADSTS50011) and failed specifically at the in-browser token-redemption step.

**Why not just fix the Entra app registration**: the user was firm this had to be solved entirely in this
app's own code, since a colleague has SSO genuinely working today in their own Angular app against this exact
same shared registration. That's real, useful evidence, not a contradiction: their app must not be doing
browser-side redemption either - a "Web"-platform registration is exactly what a server-side (confidential-
client) OAuth exchange expects. So the fix adopted is a server-side authorization-code exchange, using the
client secret already sitting in this app's config - the same fundamental pattern NextAuth/any traditional
server-rendered web app uses, just implemented in this app's own Azure Functions backend instead of assuming a
portal change. Still 100% real Entra SSO from the user's perspective (same Microsoft login page, same tenant,
same account) - only the internal plumbing of the last step moved from the browser to the server.

- **New backend endpoints** (`EntraAuthFunctions.cs`): `Auth_EntraLogin` (`GET /api/auth/entra-login`) mints a
  CSRF `state`, stores it in a short-lived `HttpOnly`/`SameSite=Lax` cookie, 302s to Entra's `/authorize`.
  `Auth_EntraCallback` (`GET /api/auth/callback/microsoft-entra-id` - the exact already-registered redirect
  URI, now a real backend route instead of being proxy-bypassed to Angular's `index.html`) validates the state
  cookie, exchanges the code via a new `IEntraAuthService`/`EntraAuthService` (MSAL.NET's
  `IConfidentialClientApplication`, `Microsoft.Identity.Client` 4.88.0 pinned explicitly in
  `TimeSheet.Infrastructure.csproj`), resolves/auto-links the app `User` by email (same rule as the existing
  per-request Entra path - deliberately duplicated rather than shared, to avoid any risk to that already-
  working code), and mints a session token via a new `ILocalAuthService.IssueSessionToken(User)` (the JWT-
  construction code extracted out of `LocalAuthService.LoginAsync`, so an Entra-derived session and a local-
  password session are byte-identical `"LocalBearer"` JWTs afterward - fully indistinguishable to
  `CurrentUserMiddleware` and everything downstream). Redirects to `/auth/complete#token=...&expiresAtUtc=...`
  (URL fragment - never sent to/logged by any server) on success, or `/login?error=...` on failure.
  `CurrentUserMiddleware` needed exactly one addition: both new routes bypass auth the same way
  `/api/auth/local-login` already does (neither request ever carries a bearer token) - a 4-line addition to an
  already-existing pattern, nothing else in that file changed.
- **A real, separate bug found and fixed live, not by design**: the very first live attempt correctly completed
  the whole OAuth exchange (no more AADSTS9002326) but returned "not provisioned" for the user's own account,
  which should have auto-linked. Root cause, confirmed by temporary debug logging then reverted: Entra's
  `preferred_username` claim came back as `Mark.Llewellyn@svgit.co.uk`, but the `Staff` table stores
  `mark.llewellyn@svgit.co.uk` - `UserRepository.GetByEmailAsync`'s plain `==` comparison is case-sensitive in
  SQLite by default, so the two never matched. **Fixed generically** (`u.Email.ToLower() == email.ToLower()`),
  not special-cased for this one row - this was a latent bug in the *existing* `CurrentUserMiddleware` auto-
  link path too (same method, same casing assumption), just never triggered before since nothing had
  exercised a real Entra login with mismatched casing until now. Also fixes any future local-login email-
  casing mismatch, for the same reason.
- **MSAL Angular removed from the frontend entirely** (`@azure/msal-angular`, `@azure/msal-browser` dropped
  from `package.json`; `msal.factories.ts`/`msal-auth.interceptor.ts` deleted; `main.ts`/`app.config.ts`/
  `current-user.service.ts`/`auth.guard.ts` simplified) - approved explicitly, since it's now genuinely dead
  weight: MSAL's only remaining job would have been the browser-side redemption that doesn't work for this
  registration. `signInWithMicrosoft()` is now a plain `window.location.href` navigation to
  `/api/auth/entra-login` (not an HttpClient call, which would follow the redirect chain in the background
  instead of navigating the tab). New `entra-complete-page` (routed at `/auth/complete`, public/no guard)
  reads the token from the URL fragment and calls the same `LocalAuthService.setSession(...)` local login
  already uses - Entra-derived and local sessions are now genuinely one mechanism end-to-end, not two parallel
  ones. `proxy.conf.js`'s special-case bypass for the callback path was removed (it's a real backend route now).
  Bundle size dropped ~270KB confirming MSAL is fully gone.
- Backend and Angular build clean throughout, 44/44 tests pass (unchanged - the only behavior change with test
  coverage, `GetByEmailAsync`'s case-insensitivity, wasn't given a dedicated new test, consistent with this
  codebase's bar for thin repository-method fixes). API host restarted three times this session for the new
  endpoints/config/case-fix in turn - confirmed clean startup and the two new Functions registered each time.
- **Live-verified in the browser, both paths, side by side, in the same session**: (1) Entra SSO - clicked
  "Sign in with Microsoft", completed a real Microsoft login (the user's own credentials/MFA - not something
  this session could do on its own), landed on `/timesheet` with `/api/me` returning 200 and real data loaded.
  (2) Local login - reset James O'Brien's password via the Staff page for a fresh one-time temp password (same
  no-real-password-handling pattern prior sessions used), signed in as him locally, confirmed his own data and
  the correct regular-user nav layout. Both fully independent and unaffected by each other.
- **Config**: `local.settings.json` gained `AzureAd:ClientSecret` (same value as the pre-existing
  `GraphAdmin:ClientSecret` - confirmed by the user to be the same underlying app registration),
  `AzureAd:RedirectUri`, `Frontend:BaseUrl`. Mirrored into `local.settings.json.example` with placeholders.
  Frontend `environment.ts`/`environment.development.ts`(`.example`) lost their `entra: {...}` block entirely -
  the frontend no longer talks to Entra directly at all, so it needs no Entra config of its own.
- **Not touched, deliberately**: the pre-existing `"Bearer"` (Entra token validation via
  `AddMicrosoftIdentityWebApi`) JWT scheme in `Program.cs`, and `CurrentUserMiddleware`'s own Entra-token/oid
  auto-link branch, are both left fully in place even though they become unreachable in practice now (the SPA
  never again sends a raw Entra access token to the API) - removing them is a safe, optional future cleanup,
  not part of this fix. Logout stays app-only (confirmed with the user) - doesn't also end the tenant-wide
  Entra session, matching how local logout already behaves.

## Done in this session, 2026-09-07 — PM flag-indicator click-through (last narrow gap in that area)

The FDD-numbered backlog is still down to just Blob Storage (parked, see below), so this round picked the one
concrete, well-scoped item off the "known loose ends" list rather than a business-decision one (EntryType rows
across more projects, the unreproduced "own entries missing from search" report) or an externally-blocked one
(Blob Storage, `User.Read.All` consent) — asked the user to choose between these via `AskUserQuestion` rather
than guessing, and this is what they picked.

- **A Project Manager can now click the "⚑ Flagged" indicator on their own Log Time grid** and land on the
  Entry Flags page, same as an Admin. Previously this only worked for Admins (`log-time-page.ts`'s
  `cellClass`/`goToFlag` both gated on `currentUser.isAdmin()` alone) even though a PM has been able to reach
  the Entry Flags page via top-level nav since the nav reorg (`2aa6693`, 2026-09-03) and the backend
  (`EntryFlagsFunctions`, gated `RequireAdminOrProjectManager`) already scopes a PM to their own managed
  projects' flags correctly — the click-through was the one place still checking the wrong (narrower) gate.
  Fixed by extracting a `canOpenFlag()` helper (`isAdmin() || isProjectManager()`) used by both the cell's
  clickable-styling check and the actual navigation guard, instead of duplicating the two-role check inline.
- Confirmed via code review that this can't land a PM on a broken/empty page: `EntryFlagsFunctions`'s own doc
  comment and every one of its endpoints already call `RequireAdminOrProjectManager(entry.Project!)`, the same
  precedent `EntryFlags_SearchEntries` follows (see the 2026-09-04 entry below) — a PM clicking through to
  `/admin/entry-flags?flagId=...` gets that flag if it's on one of their own managed projects, exactly like
  reaching the page via nav and finding it in the list normally would.
- Angular build clean. Frontend-only change, no backend/DB change, no API host restart needed.
- **Not live-verified this round** — Claude in Chrome's extension reported not connected this session (the user
  hadn't started it), so this is a code-review-only pass, not a real click-through as Sarah Chen (the PM used
  for this kind of check in earlier sessions, per the 2026-09-04 nav-check entry). Worth a quick live pass next
  time Chrome is connected, same as the "worth a quick pass" pattern this file has flagged before for other
  nav-adjacent changes.
- Dev servers were NOT running at the start of this session (nothing survived) — started fresh: Azurite (data
  dir `C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\azurite\`), API host on `:7071` (`func start` from
  `src/TimeSheet.Api`, confirmed 0 build errors and a clean startup log — the only exceptions logged are the
  pre-existing, expected-in-local-dev `DailyTimesheetReminder` timer's Graph email-send failure, unrelated to
  this change), Angular on `:3000` (`npm start`). All three left running at the end of this session.

## Done later again in this session, 2026-09-04 — Entry Flags ID search, user-facing handbook, committed

Follow-up to the round below, prompted by two things the user raised after trying the new picker live: their own
logged entries seemed to be missing from a search, and a request to also search by the raw Entry ID ("log
number").

- **Investigated "my own entries don't show up in the search"**: could not reproduce it. Tested live as Admin with
  two different terms ("Llewellyn", "Acme") - the signed-in user's own entries appeared correctly both times (see
  screenshots taken during the session). No code path excludes the searcher's own entries. Best remaining
  explanation: the picker's 25-result cap (most-recent-first) could push an older entry of the user's own past the
  cutoff on a broad search term - not confirmed, since it couldn't be reproduced directly. **Not fully closed** -
  if it recurs, the exact search term used is needed to pin it down for real.
- **Entry Flags search now also matches by exact Entry ID**, which sidesteps the above regardless of its cause.
  `TimesheetEntryRepository.SearchForFlaggingAsync` now OR's in `e.Id == entryId` when the search term parses as
  an integer, alongside the existing staff/client/project name match - an ID match is exact, not subject to the
  recency-ordering/cap issue a broad text search has. `EntryFlags_SearchEntries`'s minimum-length gate now allows
  a single-digit numeric search (a real Entry ID could be that short) while keeping the 2-character minimum for
  text searches. Placeholder text updated ("...or Entry ID…"). Backend/Angular build clean, 44/44 tests pass
  (unchanged). API host restarted (existing Function's body changed, not just hot-reloaded, per this file's own
  "don't trust an edited Function without a restart" gotcha) - clean startup. **Live-verified**: searching "42"
  returns exactly that one entry (Sarah Chen / Data Quality Audit), regardless of how many other entries exist.
- **New end-user documentation**: `Resources/TimeSheet-Handbook.html` - a self-contained, role-organized guide
  (Everyone / Project Manager / Admin) covering every screen in the app, written against the live app toured this
  session (not the FDD, which is a requirements spec, not a usage guide). This is the first user-facing
  documentation this repo has ever had - previously just this HANDOFF file (internal/dev-only) and Angular's own
  boilerplate README existed. Opens directly in a browser (no build step, no server) since it's plain HTML/CSS/
  inline JS with no external dependencies beyond a Google Fonts stylesheet link.
- **All of the above, plus the impersonation-dropdown fix and Entry Flags picker from the round below, committed
  together** as `d67167e` ("Fix impersonation dropdown positioning; add Entry Flags picker with ID search; add
  end-user handbook") - 10 files. Not pushed anywhere: **this repo has no git remote configured at all**
  (`git remote -v` returns nothing, `master` tracks no upstream) - worth knowing before assuming `git push` will
  ever do anything in this repo without first being asked to add one.

## Done even later still in this session, 2026-09-04 — impersonation-dropdown fix, Entry Flags picker, data cleanup

User asked for exactly 3 of the 5 items flagged at the end of the previous round (Blob Storage still parked, no
Azure Storage account): the impersonation-dropdown bug just found, the `EntryFlagsFunctions.RaiseManual` raw-Id
picker, and general data/environment cleanup.

1. **Fixed: impersonation dropdown's real bug** (not a color/CSS issue as first suspected - a positioning bug).
   The trigger button sits near the **left** edge of the header row (before theme/notifications/profile), but
   its panel used `right-0` (`app.html`) - meant for a right-edge trigger like the Admin dropdown's `left-0`
   counterpart - so the 256px-wide panel rendered almost entirely off-screen to the left, leaving only a ~60px
   clipped sliver visible. Changed to `left-0`, mirroring the Admin dropdown's own positioning. One-line fix.
   Live-verified: dropdown now renders fully visible, correctly positioned, all three names readable.
2. **Entry Flags "raise a flag" now has a real picker**, replacing the raw numeric Timesheet Entry Id input.
   - New `ITimesheetEntryRepository.SearchForFlaggingAsync(searchText, projectIds, take, ct)` - free-text search
     over staff/client/project name, most-recent-first, capped at `take` (25) since it's a typeahead result set,
     not a full list. Deliberately NOT filtered to unapproved entries like `GetPendingApprovalAsync` - a flag can
     be raised against any entry regardless of approval state.
   - New `EntryFlags_SearchEntries` endpoint (`GET entry-flags/search-entries?search=...`), same PM-scoping
     precedent as `TimesheetApprovalFunctions.ResolveScopeAsync`: Admin sees everything, a PM is restricted to
     their own managed project(s) via `IProjectRepository.GetManagedByUserAsync`, a PM managing nothing gets an
     empty result rather than a 403. New `EntryFlagSearchResultDto`.
   - Frontend (`entry-flags-page`): explicit-action search box (matching the existing tenant-directory-search
     convention in `users-list-page.ts` - not live-as-you-type) + results list; clicking a result selects it,
     showing "Entry to flag: #id · Staff — Client/Project · Date" with a "Change entry" link, then Notes +
     Flag Entry as before. `raiseEntryId` signal removed entirely, replaced by `selectedEntry`.
   - Backend and Angular build clean, 44/44 tests pass (unchanged - thin CRUD/search wiring, matching this
     codebase's existing bar). API host restarted (new Function definition, confirmed via HANDOFF's own
     "`func start` doesn't hot-reload new Functions" gotcha) - clean startup, no errors.
   - **Live-verified end-to-end**: searched "Sarah", selected a real entry (#42), flagged it with notes, saw it
     appear in the flagged-entries table with the right client/project/reason/notes, then cleared it again to
     leave no artifact.
3. **Data/environment cleanup**:
   - **RateCard defaults added** for Consultant ($120/hr) and Director ($200/hr) - both had zero "Default (all
     clients/projects)" row, meaning role-based fallback resolution failed for any staff/client/project combo
     not already covered by a narrower client/project-specific or person-level override. Senior Consultant
     already had a default row ($100/hr, 0.5% discount) - nothing added there. Placeholder dev-data figures. Not
     the case that ALL rate cards were missing as the old HANDOFF note implied - Director actually already had
     two narrower overrides (D365 Migration project: $150, Everlast client: $160), just no fallback default.
   - **EntryType rows added** to ERP Migration Phase 2 (project 3): Development, Testing, Documentation - the
     first project in the app to have any. Live-verified the Add Entry page's Entry Type picker now actually
     appears (it's conditionally rendered - invisible entirely when a project has zero EntryType rows) and lists
     all three.
   - **Everlast reverted to OneOff billing** (was switched to Monthly last session purely to demo
     `MonthlyBillingRollForward`, per that session's own flagged "decide before treating this as fully done"
     note). Invoice #10 (the real Draft it generated) was left alone - reverting the billing-period going
     forward doesn't retroactively undo a real draft invoice, nor was that asked for.
   - **Entry #23 and #119 fully reverted** - genuinely necessary since both were locked by the app's own guards
     (`ApprovedPayroll || SentToPayroll` blocks Edit/Delete) with no "unapprove" endpoint anywhere in the app.
     Used a small scratchpad EF Core console tool (referencing `TimeSheet.Infrastructure` directly, not raw SQL,
     so column mapping is guaranteed correct) to load and mutate the two rows via the real `TimesheetDbContext` -
     **flagged to and explicitly approved by the user first**, since this bypasses the app's API/business-logic
     layer entirely. API host stopped first to avoid concurrent SQLite writers, restarted after.
     - Entry #23: cleared `ApprovedPayroll`/`ApprovedByStaffId`/`ApprovedByName`/`DateApprovedPayroll`/
       `PostingBatch`. **Found something the previous session's HANDOFF note didn't mention**: entry #23 was
       also `SentToPayroll = true` (not just approved) - the first revert pass only cleared the Approved fields,
       leaving it in an invalid "sent but not approved" state the app's own guards would never normally produce.
       Caught and fixed in a second pass (cleared `SentToPayroll`/`SentByStaffId`/`SentByName`/
       `DateSentToPayroll` too). Confirmed back in the normal Approval Queue afterward (search "ERP" - queue
       count rose from 24 to 25 with the revert).
     - Entry #119: deleted outright (its own description said "safe to delete"). **Its August 2026
       `PayrollPeriod` row could NOT simply be deleted too** - live-checked first and found it actually
       aggregated **2 staff, 4.0h total**, not just entry #119's "1 staff, 3.0h" as the old HANDOFF note said -
       a second, legitimate staff member's real OOH hours were mixed into the same period. Deleted only the
       `TimesheetEntry` row (no FK from `PayrollPeriodLine` to `TimesheetEntry` - it stores per-user aggregated
       totals only, confirmed by reading the entity), then used the existing "Run Now" button on Payroll
       Periods to let `IPayrollAggregationService` rebuild the period's Lines in place from scratch (its own doc
       comment confirms re-running is idempotent-by-design for exactly this reason) - correctly recomputed to
       **1 staff, 1.0h, $210.00**, cleanly excluding the deleted entry while preserving the other real data.
   - Backend and Angular build clean throughout, 44/44 tests pass (unchanged).

**Live-verified in the browser**: all of the above confirmed working end-to-end during this same session (nav
fix, Entry Flags picker's full raise→clear cycle, RateCard/EntryType additions reflected immediately in the Add
Entry picker, Everlast's Billing Period field, the Approval Queue count, and the Payroll Periods page's rebuilt
August 2026 row).

## Done later still in this session, 2026-09-04 — live browser pass on nav + password-reset audit log

Claude in Chrome connected this time, so both items flagged below as "code review only, not live" got a real
live pass. Dev API host had to be started first (only Azurite + Angular had survived from earlier in the
session; `func start` from `src/TimeSheet.Api` — confirmed clean startup, no errors).

- **Per-role nav, now live-verified for all three roles** (Admin/PM/regular User), not just code review. Admin
  session (the user's own login) confirmed: full flat nav (Log Time/Calendar/Your Overview/Export/Approvals/
  Entry Flags/Payroll Periods/Invoicing/Reports) plus an "Admin ▾" dropdown with exactly the 7 expected items
  (Audit Log, Clients, Projects, Rate Cards, Roles, Settings, Staff), no "My Invoices". PM and regular-User
  sessions needed separate logins - impersonation was tried first as a shortcut but confirmed **not** to work
  for this (it only affects "log time on behalf of", the top nav still reflects the actual signed-in identity,
  not the impersonated one) - see the bug note below. Used the Staff page's own "Reset Password" to generate
  one-time temp passwords for James O'Brien and Sarah Chen so the user could sign in as each without me ever
  handling a password myself (I can't type passwords into any field, hard rule, regardless of source).
  - **Sarah Chen (PM on project 3)**: nav showed Log Time/Calendar/Your Overview/Export/Approvals/Entry Flags/
    My Invoices - no Payroll Periods/Invoicing/Reports/Admin. Clicked into both PM-visible admin-ish pages to
    confirm they're not just visible but functional: Approvals loaded her "Approval Queue (24)" scoped to her
    managed project (ERP Migration Phase 2); Entry Flags loaded normally ("All clear - no open flags").
  - **James O'Brien (regular User)**: nav showed only Log Time/Calendar/Your Overview/Export/My Invoices -
    everything Admin-or-PM-gated correctly absent.
  - This fully confirms and closes the "worth a quick pass" flag from the nav-reorg entry below, now genuinely
    verified live rather than by code review alone, matching what the code review had already found.
- **Password-reset audit log, live-verified.** Reset James O'Brien's password from the Staff page (Admin
  session) - confirmed the `ConfirmService` dialog shows correctly (not a native `confirm()`), a one-time temp
  password banner appears on success, and a new `User.PasswordReset` row appears in Admin → Audit Log with the
  correct entity (`User #3`) and details (`james.obrien@svgit.co.uk (local)`). This is the live confirmation
  the previous entry's fix (`AdminUsersFunctions.ResetPassword` now writes `AuditLog`) didn't get at the time.
- **Real bug found, not yet fixed**: the impersonation dropdown ("Log time on behalf of another user", the
  swap-arrows icon top-left of the nav) renders its option list with invisible text - confirmed via
  `read_page`'s accessibility tree that the options (James O'Brien/Priya Patel/Sarah Chen) are genuinely there
  and clickable, but a screenshot/zoom shows an empty-looking dark box with no visible text (likely dark-on-dark
  CSS, not a rendering failure - the tree proves the DOM content exists). Not fixed this round - flagging for
  next session.
- **Left-over demo-data artifact**: James O'Brien's and Sarah Chen's local-account passwords were both reset
  during this pass (Sarah Chen once, James O'Brien twice) so the user could sign in as each to check the nav -
  the pre-reset passwords are gone, only the last one-time temp password shown for each still works (and both
  should probably be changed again to something the user will actually remember, since the temp passwords were
  only ever shown transiently on screen). Same "clearly attributable and reversible" category as prior
  sessions' verification leftovers.
- Dev servers: Azurite + Angular were already running from earlier in the session; API host was (re)started
  this round - all three left running at the end.

## Done in this session, 2026-09-04 — HANDOFF cleanup, per-role nav check, one small audit-log fix

The FDD-numbered backlog was already down to one item (Blob Storage) coming into this session, so this round
was mostly about closing out the "Known loose ends" list from the bottom of this file rather than finding new
gaps - several of those turned out to be stale.

- **Re-confirmed with the user: Blob Storage stays parked.** Still no Azure Storage account - not revisited.
- **Two stale loose-end notes corrected, not re-fixed** (they were already fixed, the note just never got
  updated): "PM can't reach Entry Flags" was actually resolved by the nav reorg (`2aa6693`, the previous
  session) - confirmed by reading current `app.html`/`app.routes.ts`. "`ProjectsAdminService.listByClient` uses
  backslashes" doesn't match the current code (forward slashes) or any point in the file's `git log -p`
  history - whatever this referred to no longer exists (or never did).
- **Per-role nav check done by code review, not a live browser pass** (Claude in Chrome wasn't connected this
  session - the user started installing it but didn't finish). Cross-checked every `app.html` nav item's `@if`
  condition against its route's guard in `app.routes.ts`: fully consistent for Admin/PM/regular User, no dead
  links, no nav-hidden-but-guard-open gaps beyond the one already-deliberate `My Invoices` case (hidden from
  Admins since they have the full Invoicing page instead). This closes the "worth a quick pass next session"
  flag from the previous session's nav-reorg entry.
- **Fixed: `AdminUsersFunctions.ResetPassword` now writes a real `AuditLog` row**, not just an `ILogger` line.
  The dedicated `AuditLog` table this note said would be "a natural future enhancement" already existed (added
  the session before last) - the comment just predated that migration and nobody had come back to wire this one
  Function into it. Added the same `IAuditLogService`/`IUnitOfWork` pattern already used by
  `EntryFlagsFunctions`/`TimesheetEntriesFunctions`/etc.: a `User.PasswordReset` row (entity `User`, details =
  target email + local/SSO). The pre-existing `ILogger.LogWarning` call was left in place alongside it.
- Backend builds clean, 44/44 tests pass (unchanged - no new test, matching this codebase's existing bar for
  thin audit-logging wiring). API host restarted for the new constructor dependencies, confirmed no startup
  errors. Dev servers (Azurite, API on `:7071`, Angular on `:3000`) all started fresh this session and left
  running.
- **Not touched, deliberately** - the rest of the "known loose ends" list is either genuinely external
  (`User.Read.All` Entra consent, no Azure Storage account), environmental/data setup (no default RateCard
  rows for the newer roles, no `EntryType` rows on any project yet), or a real but small UX nice-to-have
  (`EntryFlagsFunctions.RaiseManual`'s raw-Entry-Id input instead of a picker) that wasn't asked for this round
  - flagging it again below in case it's wanted next.

## Done still later in this session, 2026-09-03 — loading-state consistency pass (20 pages)

Not from the FDD - a UI polish item the user asked for. A pre-work survey of every routed page component
found that **zero of the app's 26 pages showed any indicator during their initial data fetch** - and worse, 7
of them (Clients list, Projects list, Entry Flags, Approvals' both tabs, Roles, Audit Log, Payroll Periods)
briefly showed a **false "no data" empty-state message** before the real data arrived, because their
empty-state check (either a manual `.length === 0` or Angular's `@for...@empty` block) had no loading gate at
all. That's a real, user-visible bug, not just a missing spinner - a genuinely-populated list could flash "No
clients yet" / "Nothing waiting for approval" / etc. for a moment on every page load.

- **New shared `LoadingSpinner` component** (`core/components/loading-spinner/loading-spinner.ts`) - a small
  centered spinner + label, mounted via `@if (loading()) { <app-loading-spinner /> } @else { ...content }`.
  Established as the one consistent convention across the whole app rather than inventing a per-page treatment.
- **Convention**: a page-level `loading`/`loaded` boolean signal (name matched whichever the page already used,
  where one existed), defaulting to whichever state is accurate before the first fetch resolves. For pages
  whose `refresh()`/fetch method is also called again later (search-as-you-type, post-action reload, tab
  switch), the signal is **only ever flipped true→false once, on the first call, and never reset** - so
  re-fetching after an action doesn't tear down and re-flash a spinner over already-visible data. Documented
  inline on each such signal.
- **Edit/detail pages** (Client edit, Project edit, and Add/Edit Entry in edit mode) gate on the specific
  record's own fetch, not just a supporting dropdown list - a brand-new (create-mode) record's form is always
  immediately usable, since there's nothing to wait for.
- **20 pages touched**: Clients list, Projects list, Entry Flags, Approvals (both tabs), Roles, Audit Log,
  Payroll Periods (the 7 false-empty-state bug fixes above), plus Your Overview, My Invoices, Projects (all),
  Settings, Add Expense, Add Entry, Calendar, Staff list, Log Time, Project Assignments, Project Entry Types,
  Client edit, Project edit (13 more that previously showed nothing/an empty table during load).
- **Deliberately skipped**: Reports, Invoicing, Rate Cards, Export - their only fetch on page load populates a
  filter dropdown, not a data table; the actual report/invoice/export content only appears after an explicit
  user action (Run/select/Download), which already has its own feedback (button text, `downloading`/`saving`
  signals). A full-page spinner for a dropdown populating would be overkill, not a fix for anything broken.
- **Missing error handlers fixed along the way**: several pages' initial fetch had no `error` callback at all,
  so a failed request would leave `loading`/`loaded` stuck and the page hung forever with no explanation.
  Added consistently, with **one deliberate exception**: Settings' fetch failure does NOT flip `loaded` true,
  because that page lets you Save over whatever it's showing - revealing the form with blank/default field
  values on a load failure (and letting Save silently overwrite the real saved settings with them) would be
  actively harmful, unlike a list page harmlessly showing empty. The spinner there is gated on
  `!loaded() && !error()` specifically so an error stops the spinner without ever revealing the form.
- Angular build clean after every batch (built incrementally through the whole pass, not just once at the
  end). Frontend-only change, no backend impact, no API host restart needed.

**Live-verified in the browser by the user**: confirmed the fixes are in place, though the local dev API
responds fast enough that the spinner itself is barely visible without throttling the network in DevTools -
the more meaningful verification is the false-empty-state bug fix (code-reviewed, not separately re-triggered
live) and the build passing cleanly through 20 pages' worth of template changes.

## Done even later still in this session, 2026-09-03 — replaced native confirm() dialogs, new shared ConfirmService

Not from the FDD - general UI polish requested by the user after the nav reorg, starting with the smallest,
most contained item: three places still used the browser's native `confirm()` popup (Log Time's delete entry,
Clients' deactivate, Staff's force-password-reset) - the same "feels out of place" issue already fixed for
Entry Flags' clearing-notes `prompt()` earlier this session.

- **New `ConfirmService`** (`core/services/confirm.service.ts`) + **`ConfirmDialog`** component
  (`core/components/confirm-dialog/confirm-dialog.ts`), mounted once in `app.html` (`<app-confirm-dialog />`,
  alongside `<router-outlet>`) rather than three separate bespoke inline-UI implementations - a modal overlay
  works uniformly regardless of the underlying list technology (AG-Grid cell renderer for Log Time vs. plain
  HTML table rows for Clients/Staff), which an inline-expanding-row approach (like Entry Flags' clearing-notes
  fix) couldn't do consistently across all three.
- `ConfirmService.confirm(message, { confirmLabel?, destructive? })` returns a `Promise<boolean>` - callers do
  `const ok = await this.confirmService.confirm(...); if (!ok) return;` in place of the old
  `if (!confirm(...)) return;`. `destructive: true` renders the confirm button as `.btn-danger` instead of
  `.btn-primary`, used for all three current call sites since delete/deactivate/reset-password are all
  destructive-ish actions.
- Updated: `log-time-page.ts` (delete entry), `clients-list-page.ts` (deactivate), `users-list-page.ts`
  (force password reset). All three methods became `async` to `await` the new promise-based confirm.
- This is reusable infrastructure now - any future native `confirm()`/`prompt()` replacement should use
  `ConfirmService` rather than building another bespoke inline UI.
- Angular build clean. Frontend-only change, no backend impact.

**Live-verified in the browser by the user**: confirmed looking good.

Other UI polish flagged but not yet started: most pages show no loading indicator while their initial data
fetch is in flight (blank/empty flash instead) - a much bigger, more invasive pass than this one since it
touches dozens of pages rather than three call sites. Not started; ask the user before picking this up given
the scope.

## Done even later in this session, 2026-09-03 — top nav reorganized (chronological + tidier)

Not from the FDD re-audit - the user found the top nav bar messy (16-17 identically-styled pills in one flat
wrapping row for an Admin, with no grouping and no relation to the app's actual workflow order) and asked for
it to flow in chronological order and look tidier. Used Plan Mode for this one given the genuine design
tradeoffs involved (grouping strategy, dropdown vs. flat, what to bundle in).

- **Reordered the top-level bar to match the app's real business workflow** (Log Time → Approve → Payroll →
  Invoice → Report, confirmed via route comments/this file's own history, matches the FDD): Log Time, Calendar,
  Your Overview, Export, Approvals, Entry Flags, Payroll Periods, Invoicing, Reports, then the new Admin
  dropdown - replacing the old order where Approvals was rendered last and Payroll/Invoicing/Reports were
  scattered mid-list among unrelated config links.
- **New "Admin ▾" dropdown** groups the 7 non-chronological "one-time setup" config items (Audit Log, Clients,
  Projects, Rate Cards, Roles, Settings, Staff - alphabetical, since there's no chronology among them) behind
  one trigger, reusing the exact `.relative`/toggle-signal/`.card.absolute` pattern this app already used twice
  (notifications and impersonation dropdowns) - no new CSS needed. Cuts the Admin view's flat item count from
  ~16 down to ~10.
- **Fixed the previously-flagged "PM can't reach Entry Flags" gap** (see the loose-end note added earlier this
  session) as part of the same work, since it touched the same `@if` conditions anyway: pulled "Entry Flags" out
  of the Admin-only dropdown into a flat top-level link visible to Admin-or-PM (alongside Approvals, the other
  Admin-or-PM item), and removed `adminGuard` from the `admin/entry-flags` route in `app.routes.ts` (kept
  `authGuard` only) - mirroring `admin/approvals`'s own existing precedent exactly, since the backend
  (`RequireAdminOrProjectManager`) already scopes a PM to their own projects' flags correctly. Verified
  `entry-flags-page.ts`/`.html` have no hardcoded admin-only UI branches, so no page-level changes were needed.
- **Added click-outside-to-close for all three dropdowns** (notifications, impersonation, the new Admin menu) -
  none of them closed except via re-clicking their own toggle button before this. One shared
  `@HostListener('document:click', ...)` on `App` checks `event.target.closest('[data-dropdown="..."]')` against
  a `data-dropdown` attribute added to each dropdown's wrapper, rather than three separate near-duplicate
  listeners.
- **New `isAdminSectionActive` computed signal** makes the "Admin" trigger itself show an active/highlighted
  style when the current route is one of its 7 dropdown items, even while the dropdown is closed - a plain
  `routerLinkActive` on the trigger can't do this, since the dropdown's `<a>` tags are fully unmounted via `@if`
  while closed, and `RouterLinkActive`'s `ContentChildren` query has nothing to find in that state.
- Angular build clean. Frontend-only change (plus the one route-guard line in `app.routes.ts`) - no backend
  changes, no API host restart needed.
- **Follow-up in the same sitting**: the user separately noticed the main content area (header + `<main>` in
  `app.html`) was capped at `max-w-7xl` (1280px), leaving visible whitespace either side of wide content like
  the Log Time grid. First tried widening both to a bigger fixed cap (`max-w-[1600px]`) - checked first that
  every form/detail page (client/project edit, settings, add entry/expense, export) already imposes its own
  narrower `max-w-2xl`/`max-w-3xl` wrapper internally, so widening the shared outer container only affects
  pages (grids/lists) that don't already self-constrain, no risk of stretching a form layout. **Then the user
  moved to a larger monitor and hit the same whitespace problem again at the new, bigger fixed number** - asked
  for a "resize option" rather than another guessed cap. Removed the `mx-auto`/`max-w-*` constraint from both
  the header and `<main>` entirely (now just `flex flex-wrap items-center justify-between gap-y-2 px-6 py-3` /
  `p-6`) - the content area now always fills the actual browser width with consistent side padding, on any
  screen size, rather than capping at a number that's inevitably wrong for someone else's monitor.

**Live-verified in the browser by the user**: the fully-fluid width change confirmed looking correct on both a
laptop screen and a larger monitor. The nav reorg itself wasn't explicitly walked through against the full
per-role checklist below (Admin/PM/regular User) - the user moved on to the width issue before confirming each
item - worth a quick pass next session if it hasn't come up by then.

**Per-role nav check done 2026-09-04, by code review not live browser** (Claude in Chrome wasn't connected this
session): cross-checked every `app.html` nav item's `@if` visibility condition against its route's guard in
`app.routes.ts`. Result - fully consistent, nothing to fix: `Log Time`/`Calendar`/`Your Overview`/`Export` show
unconditionally and their routes carry only `authGuard`; `Approvals`/`Entry Flags` show for
`isAdmin() || isProjectManager()` and their routes also carry only `authGuard` (by design - the backend scopes
a PM to their own managed projects rather than the route blocking them, per each route's own comment);
`My Invoices` shows only for `!isAdmin()` even though its route has no `adminGuard` either - deliberate, since
an Admin already has the full `/admin/invoicing` page and doesn't need the PM-scoped personal view; everything
under the `Admin ▾` dropdown plus `Payroll Periods`/`Invoicing`/`Reports` shows only for `isAdmin()` and every
one of those routes does carry `adminGuard`. No dead nav links, no nav-hidden-but-guard-open gaps beyond the
one already-documented, deliberate `My Invoices` case. This closes the "worth a quick pass" flag from the
previous entry - genuinely done now, just via reading the code rather than clicking through three logins.

## Done last in this session, 2026-09-03 — Project Health removed entirely

Follow-up to the very next entry below: after being told the Anthropic API isn't free and given the earlier
finding that the feature doesn't demonstrably trace to any FDD text (see that entry's "Investigated but NOT
fixed" note), the user decided to just remove it rather than pay for or keep an unverified feature around.

- **Deleted entirely** (not disabled/hidden): `ProjectHealthAssessment` entity, `IProjectHealthService`/
  `ProjectHealthService`, `IProjectHealthAssessor`/`ProjectHealthAssessor`, `IHealthAnalysisClient`/
  `ClaudeHealthAnalysisClient`, `IProjectHealthAssessmentRepository`/`ProjectHealthAssessmentRepository`,
  `ProjectHealthFunctions.cs`, `ProjectHealthTimerFunction.cs`, `ProjectHealthDtos.cs`, the Angular
  `project-health-page`/`project-health.service.ts`, the `admin/health` route, and the "Project Health" nav
  link. New migration `RemoveProjectHealth` drops the `ProjectHealthAssessments` table and
  `Project.LatestHealthAssessmentId` column (both originally added together in the old
  `20260826110920_LegacySchemaAlignment` migration - never edit an already-applied historical migration, add a
  reverse one instead).
- **Deliberately did NOT remove** `NotificationType.ProjectHealthDeclined` (`Enums.cs`) or its title-mapping
  case in `NotificationService.cs` ("Project health alert") - that enum is persisted via `HasConversion<string>()`,
  so deleting the member would break deserialization of any pre-existing `Notification` row of that type. This
  exact situation already had a precedent in the same enum: `EscalationRaised`/`EscalationDecided` carry a
  comment explaining they're "Unused going forward... kept so historical Notification rows still deserialize" -
  followed that same pattern rather than inventing a new one. Nothing can produce a *new* one of these
  notifications anymore now that `ProjectHealthService` is gone, which is the only thing that mattered.
- **Confirmed NOT touched**: `IRevenueRecognitionService`/`RevenueRecognitionService` (Fixed Fee revenue
  recognition for invoicing/reporting) - an easily-confused-by-name but completely unrelated interface; the
  earlier investigation's mention of it alongside Project Health was purely about a design constraint (Fixed
  Fee revenue can't be honestly attributed to a specific role in reporting), never a code dependency.
- Full removal manifest was produced by an exhaustive repo-wide search first (every file, every reference)
  before deleting anything, specifically to avoid exactly the kind of "looks unrelated but actually breaks
  deserialization" mistake the `NotificationType` case above would have been.
- Backend and Angular build clean, 44/44 tests pass (unchanged - there were never any tests for this feature).
  API host restarted (DI/constructor changes from removing 4 service registrations); confirmed old
  `/api/projects/health/*` routes now correctly 404. New migration confirmed applied to the dev DB directly
  (both the table and column verified actually gone, not just "no error on apply").
- The item below this one ("Done in a follow-up round... Entry Flags, Project Health") describes UX fixes made
  to Project Health *before* this removal decision - all of that code and everything it fixed no longer exists.
  Left as historical record rather than deleted from this file, but don't go looking for any of it in the
  running app anymore.

## Done in a follow-up round the same session, 2026-09-03 — user-driven UX fixes (Entry Flags, Project Health)

Not from the FDD re-audit - the user spent time actually using the app after the re-audit work and found four
concrete usability problems worth fixing, plus one open question about a feature's origin that's now been
investigated (see the flag below the fix list).

1. **Entry ID now visible.** The Entry Flags "raise a flag" form has always asked for a raw numeric Timesheet
   Entry Id, but nothing anywhere in the UI ever showed that number - the Log Time grid now has an "Entry ID"
   column (`log-time-page.ts`) so a user can actually read one off.
2. **Project Health "Run Now" + accurate empty-state text.** The dashboard only ever showed projects with at
   least one existing assessment, and there was no way to create a project's first one from the UI at all - the
   empty-state text even claimed a "trigger from a project's page" button existed, which it didn't.
   `IProjectHealthService.ReassessAllActiveAsync` is new shared logic (the sweep loop moved out of
   `ProjectHealthTimerFunction` into the service, so the timer and the new `ProjectHealth_RunNow` Admin
   endpoint/button call the identical code path) - mirrors the existing `PayrollPeriods_RunNow`/
   `BillingRollForward_RunNow` convention exactly.
3. **"Notify Staff" now gives feedback.** It always worked server-side (creates a real Notification for the
   entry's owner via the same bell-icon system used elsewhere) - the button just silently ignored the response,
   `.subscribe()` with no callback at all, so it looked broken even when it worked. Now shows a success/error
   banner like every other action in the app.
4. **Flagged entries are now visible where they're actually seen.** A flag is never a gate (FDD) - editing an
   entry with an open flag was never blocked - but there was previously NO visual indicator anywhere (Log Time
   grid, Add/Edit Entry) that an entry even had one; the only way to know was the separate Admin-only Entry
   Flags screen. `TimesheetEntryDto` now carries `OpenFlags` (reusing `EntryFlagDto` - an entry can in principle
   have more than one open flag simultaneously, e.g. a system-raised budget flag alongside a manual one, so this
   is a list, not a single nullable). Log Time's grid shows a "⚑ Flagged" indicator with a hover tooltip
   (reason/notes/date, plain-text via `tooltipValueGetter` - deliberately not an interactive ag-grid tooltip
   component, to avoid the fragility of getting a clickable button working reliably inside one); clicking the
   indicator navigates to `/admin/entry-flags?flagId=...` **but only for Admins** - that page is Admin-only
   today (a PM technically has backend permission to act on flags for their own projects per
   `RequireAdminOrProjectManager`, but has no nav entry into the page at all - a separate, still-open gap, not
   fixed this round, flagged again below for visibility since it was already noted once and easy to lose track
   of). The Entry Flags page itself reads a `flagId` query param and scrolls/highlights that row when arriving
   via the click-through.
5. **"Clearing notes" replaced the native `prompt()` with a proper inline input** (a small text field + Confirm/
   Cancel appearing in place of the row's action buttons) - a `prompt()` dialog looked jarring and out of place
   next to the rest of the app's styled UI, was raised directly by the user as feeling wrong.
- Backend and Angular build clean, 44/44 backend tests pass (unchanged - all 5 fixes here are either thin
  CRUD/UI wiring or query-shape changes with no new business-logic branching, consistent with this codebase's
  existing bar for what warrants a dedicated test - matches the precedent already set for the entry-attachment
  and project-attachment features, which also have none). API host restarted for the DI/constructor changes
  (`IEntryFlagRepository`'s new method signature, `ProjectHealthService`'s new `ILogger` dependency) - confirmed
  no startup errors both times.

**Investigated but NOT fixed - a genuine, material finding worth flagging prominently**: the user asked what
part of the FDD "Project Health" satisfies, since they couldn't tell from using it. A direct search of the
extracted FDD document text (unzipped the docx, stripped XML tags) for "health", "AI", "risk", "assessment",
"Claude", "on track", and "behind schedule" - in both the main document body AND its embedded reviewer comments
- returned **zero matches for every single term**. The feature was introduced in commit `19c368d`
("AI Project Health (Requirement 8): Claude-powered nightly assessment"), whose message cites "Requirement 8"
of "the plan's phases 1-9" - but **no document defining that plan or numbering 9 requirements exists anywhere
in this repository today** (checked `.md` files repo-wide and the `Resources/Timesheets_20260824092236`
PowerApps export bundle - nothing). So: this cannot currently be traced to explicit FDD text, and its actual
origin (a separate stakeholder-agreed requirement never written into this FDD doc? a different planning
conversation from an earlier session, now lost? something added without real grounding?) can't be verified from
what survives in this repo. **This needs the user's own call**, not a guess: keep it as a nice-to-have AI
feature (once an API key is added), or deprioritize/remove it since it doesn't demonstrably trace to the FDD.
What the feature actually DOES, regardless of its origin: a nightly per-project check (bounded
concurrency, respects the Anthropic API's rate limits) that gathers each project's budget/hours-consumed/
time-elapsed data plus recent timesheet descriptions, asks Claude to classify it On Track / At Risk / Behind
with a plain-English explanation and recommended action, persists the verdict, and notifies Admins only when a
project's status *worsens* (never on every run, to avoid alert fatigue).

## Done yet still later the same session, 2026-09-03 — reporting: entry counts + role-basis breakdown

Closes the last remaining backlog item (Blob Storage stays parked - still no Azure account).

- **Entry counts** (FDD: "the number of entries..."): every existing report (all six Time/Cost/Profit
  x Project/Client reports, both their summaries and every breakdown line) now carries an
  `EntryCount` - the raw `TimesheetEntry` row count, not a day-count or hour-sum. Required widening
  `IReportingRepository.TimeEntryAggregateRow` with a `g.Count()` at the existing (Project, Client,
  User, Date) grouping stage, then `g.Sum(x => x.EntryCount)` wherever the service re-groups those
  rows further (by User or by Project) to build each report's lines.
- **Role-basis breakdown** (FDD: "...on a team, role and user basis"): 3 new reports -
  `GetTimeOnProjectByRoleReportAsync`/`CostOnProjectByRoleReportAsync`/`ProfitOnProjectByRoleReportAsync`
  - grouping the same underlying data by each staff member's current `User.JobRoleId` instead of by
  User (`RoleName` = "Unassigned" when null). New Function endpoints
  `reports/time|cost|profit-on-project-by-role`, new Angular report-type options.
  - **Deliberately scoped to "on Project" only, not "on Client"** - a Client-scoped report already
    spans multiple projects that can mix Time&Materials and Fixed Fee payment models, and Fixed Fee
    revenue is recognized at the whole-project level (`IRevenueRecognitionService`) - there's no
    honest way to slice that recognized revenue down to "this role's share of it" without either
    misrepresenting numbers or a materially bigger design. Flag for a human call if Client-scoped
    role reporting turns out to be wanted after all.
  - **"Team" was NOT addressed** - it has no entity anywhere in this app's data model, so guessing at
    what it would even mean risked inventing a feature nobody asked for. Flag for a human call if
    this matters - don't guess at what "team" means without asking first.
  - Role itself is NOT effective-dated (`Role.cs`'s own doc comment already says so) - a role-basis
    report reflects each staff member's CURRENT role, not whatever role they held on the entry's own
    date. This is the exact same accepted limitation RateCard-tier resolution already lives with, not
    a new one introduced here.
- Widening `TimeEntryAggregateRow`'s grouping key with `RoleId`/`RoleName` (via a left join from
  `User.JobRoleId` to `Role`) is provably a no-op on every OTHER existing report's row count/
  granularity: a user's role is a simple current-state fact of that user, so it can never split an
  existing (Project, Client, User, Date) group into more than one row.
- 2 new unit tests in `ReportingServiceTests.cs`: one proves `EntryCount` reflects raw entry rows (2
  separate `TimesheetEntry` rows for the same user/project/date, summed into one aggregate bucket by
  the existing grouping - only `EntryCount` can tell "2 entries totalling 10h" apart from "1 entry of
  10h"); one proves the Role breakdown groups correctly and buckets a no-`JobRoleId` user under
  "Unassigned" rather than dropping their hours.
- **A real, pre-existing UX issue found and fixed via the user's own live look at the new
  columns**: the Reports page renders whatever field names the API returns verbatim as column/tile
  headers (`Object.keys(...)` on the JSON payload) - so `entryCount` showed as literally "entryCount"
  rather than "Entry Count", and this was *already true* before this session for every existing
  column (`userId`, `workHours`, etc.) - just not noticed until a new, more obviously-database-shaped
  field name (`entryCount`) made it visible. Fixed generically (a `columnLabel()` camelCase-to-Title-
  Case formatter applied to both the summary tiles and the breakdown table headers), not just
  patched for the one new field, so it also silently improved every pre-existing report's headers.
- Backend and Angular build clean, 44/44 backend tests pass (42 pre-existing + 2 added).

**Live-verified in the browser by the user**: ran the new "(by Role)" report options, confirmed
column headers now read as human labels ("Entry Count" etc.) instead of raw camelCase field names,
across both new and pre-existing columns.

## Done still later the same session, 2026-09-03 — project-level attachments ("Documents")

Blob Storage itself stayed parked (still no Azure Storage account anywhere - confirmed again with the
user), but item 2 from the backlog below (project-level attachments) doesn't actually need it: it
reuses the same `IFileStorageService` abstraction the entry-attachment feature already uses (local
disk today; a single-class swap to Blob Storage later, whenever that account exists).

- New `ProjectAttachment` entity/table - a separate table from the entry-scoped `Attachment` (not a
  nullable-FK polymorphic row on it), matching this codebase's existing one-table-per-concept style.
  Adds an `UploadedBy` nav (entry attachments don't have this) since a project's documents are seen
  by potentially several people - Admin and PM alike - not just the one owner an entry has. New
  migration `AddProjectAttachments`.
- New `ProjectAttachments_List`/`Upload`/`Download`/`Delete` Functions, gated
  `RequireAdminOrProjectManager` (the same gate `Projects_Estimate` already uses) rather than the
  entry-attachment's ownership check - a project has no single "owner" the way an entry does.
- New "Documents" card on the project edit page (upload button, list with Download/Delete, shows
  who uploaded and when) - Angular's entry-attachment feature had upload plumbing but literally zero
  consuming UI anywhere, so there was no existing screen pattern to copy; this one was designed
  fresh from the DTO shape and the invoice-PDF-download's `blob`/`createObjectURL` pattern.
- **A real bug found via the user's own live test, not caught by the build or by review**:
  `ProjectAttachmentRepository.GetByProjectAsync` ordered by `UploadedAtUtc` (a `DateTimeOffset`)
  directly in the EF query - SQLite can't translate `ORDER BY` on that type, so every list call
  500'd. Upload itself always succeeded (file saved, row created) but the immediate refresh-after-
  upload, and every subsequent page load, silently failed - looked exactly like "nothing happened."
  This exact SQLite limitation is already worked around elsewhere in this codebase
  (`NotificationRepository`, `AuditLogRepository` both materialize to a list first, then sort
  client-side) - missed applying the same pattern here on the first pass. Fixed, plus added a
  missing error handler on the initial list-load call (it had none - a future failure there would
  have gone silent the same way, only the upload-flow error handler existed before this fix).
- No unit tests added - matches this codebase's established bar (thin CRUD Functions plus a trivial
  repository, same category as the pre-existing `AttachmentsFunctions`/`AttachmentRepository`, which
  also have none).
- Backend builds clean, Angular builds clean. API host restarted twice this session for this feature
  (once for the new endpoints/migration, once after the SQLite-ordering fix) - confirmed no startup
  errors both times, migration confirmed applied via a direct DB check.

**Live-verified in the browser by the user**: uploaded a document, hit the silent-failure bug above,
then after the fix confirmed upload → list refresh → (implicitly available) download/delete all work
without needing a manual page refresh.

## Done later the same session, 2026-09-03 — full FDD re-audit + 3 quick backlog fixes

The user asked for a full re-check of the FDD against the app (not just a rollover of the prior session's
single-item backlog), on the assumption Blob Storage was the only remaining gap. A fresh, independent read of
the whole FDD document plus cross-checking against actual code (not comments/docs) found 7 gaps total, not 1 —
see the numbered list under "What's still open" below for the full list in priority order. Tackled the first 3
(all small, self-contained, no external dependency) in this same session:

1. **`Project.CanInvoice` wired up.** `InvoiceGenerationService.BuildDraftAsync` now `continue`s past any
   project with `CanInvoice == false` before building ANY of its line items (T&M, Fixed Fee, or Expense alike) —
   previously the flag existed on the entity/DTO/UI but was never read. `null` (every project predating this
   flag) and `true` both still invoice as before — only an explicit `false` excludes a project. 2 new tests in
   `InvoiceGenerationServiceTests.cs`.
2. **Impersonation's "only active users" rule is now API-enforced, not just UI-filtered.** New
   `TimesheetEntriesFunctions.ValidateImpersonationTargetAsync(onBehalfOfUserId, ct)` (403 if the target user is
   missing or `!IsActive`), wired into every `OnBehalfOfUserId` path in that file: `List` (via a new
   `ResolveViewTargetAsync`), `Get`, `Create`, `Update`, `Delete`, `Duplicate`. New `IUserRepository users`
   constructor dependency. The Angular impersonation picker already only listed active users — this closes the
   gap where the API itself didn't check, contradicting this app's own established "enforce in the API, not
   only the UI" pattern (same principle as the entry-locking work above).
3. **Staff count on project lists.** FDD: "The project list shows a count of how many staff are assigned to
   each project." New `IStaffProjectRepository.GetActiveAssignmentCountsAsync` (one grouped query per list
   call, not N+1), new `ProjectDto.AssignedStaffCount`, a "Staff" column added to both `/admin/projects` (the
   flat all-clients list) and `/admin/clients/:clientId/projects` (the per-client list) - `projects-all-page`
   and `projects-list-page` respectively. `Projects_Get`/`Projects_Update` also corrected to return the real
   count instead of always 0 (they don't touch assignments, but the DTO field needs to be accurate regardless
   of which endpoint returned it).
- Backend builds clean, 41/41 tests pass (39 pre-existing + 2 added). Angular builds clean. API host restarted
  mid-session to pick up the DI/constructor changes (confirmed no startup errors in the log) - remember this
  next time a Function's constructor signature changes, same "func start doesn't hot-reload" caveat as before,
  just for constructor/DI shape rather than only brand-new `[Function(...)]` attributes.

**Live-verified in the browser by the user**: confirmed the "Staff" column now shows on both project list
pages after a hard refresh (Angular's dev-server hot-reload hadn't picked up the new column initially).
CanInvoice's exact semantics (`false` excludes entirely from invoicing; `null`/`true` invoice as before) were
confirmed by discussion, not a live invoicing pass, since the point was inspectable directly from the diff/tests.

Ambiguous item from the FDD re-audit, deliberately NOT on the numbered backlog list (flag for a decision if it
ever matters, not a bug): FDD describes a Client having two currencies (charge vs. invoice); `Client` has one
`CurrencyId`. A reviewer comment embedded in the FDD docx itself suggests this was likely a deliberate
simplification already agreed with the business.

Dev servers: same three as before (Azurite, API on `:7071`, Angular on `:3000`), all still running from earlier
in this session - not restarted except the API host as noted above. Committed to `master` (see commits after
`8ae1b70`).

## Done earlier the same session, 2026-09-03

Entry locking on invoice finalization (FDD: "Finalizing an invoice locks the entries it was built from") — this
was open item 2 from the previous session's list below.

- New nullable `TimesheetEntry.InvoiceId` FK (+ `Invoice` nav property), new migration
  `LockTimesheetEntriesOnInvoiceFinalize` (`RecordedTimes.InvoiceId`, FK to `Invoices`, `Restrict` delete).
- `InvoicingService.FinalizeInvoiceAsync` now calls a new private `LockEntriesAsync`: since
  `InvoiceGenerationService.BuildDraftAsync` only ever persisted an aggregated per-project sum on each Time &
  Materials line item (the individual `TimesheetEntry` rows that fed it were never recorded anywhere), locking
  has to **reconstitute** that entry set at finalize time by re-running the exact same
  `ITimesheetEntryRepository.GetCountedForInvoicingAsync(projectId, periodStart, periodEnd, ct)` query used to
  build each T&M line item, then stamping `InvoiceId` on every entry it returns. Fixed Fee and Expense line
  items don't derive from `TimesheetEntry` rows at all, so nothing is touched for those.
- `TimesheetEntriesFunctions.Update`/`Delete`/`Duplicate` (source-entry check only, same as the existing
  `ApprovedPayroll`/`SentToPayroll` guard) now also reject with 409 when `entry.InvoiceId is not null`, via a
  new `InvoicedLockedResult()` helper mirroring the existing `SentToPayrollLockedResult()`.
- `TimesheetEntryDto` gained a computed `Invoiced` bool (`e.InvoiceId is not null`); Angular's
  `TimesheetEntry` model and `entry-actions-cell.ts` follow the same pattern as the existing "sent to payroll"
  lock — the grid now shows "Locked (invoiced)" (vs "Locked (sent to payroll)") and hides Edit/Duplicate/Delete.
- 3 new unit tests in a new `InvoicingServiceTests.cs` (there was no test file for `InvoicingService` before
  this — `BuildDraftAsync`/`GenerateDraftInvoiceAsync` had coverage via `InvoiceGenerationServiceTests`, but
  `FinalizeInvoiceAsync` itself had none): a T&M entry counted into a finalized invoice's period gets locked, an
  entry outside that period is left alone, and a Fixed Fee project's entries are never touched at all (matching
  `InvoiceGenerationServiceTests`' in-memory-SQLite-plus-real-repositories style, with lightweight fakes for
  `IPdfInvoiceRenderer`/`INotificationService`). `BillingRollForwardServiceTests`' direct `new InvoicingService(...)`
  construction updated for the new constructor parameter.
- Backend builds clean, 39/39 tests pass (36 pre-existing + 3 added). Angular builds clean.

**Live-verified in the browser by the user**: generated and finalized a Draft invoice for a Time & Materials
project's entries, then confirmed those entries now show "Locked (invoiced)" in the Log Time grid with
Edit/Duplicate/Delete hidden, exactly as intended.

Dev servers were NOT left running at the end of the previous session (fresh session, no processes survived) -
this session started them fresh: Azurite (same command as before, data dir
`C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\azurite\`), API on `http://localhost:7071` (`func start`),
Angular on `http://localhost:3000` (`npm start`). Same dev SQLite file; this session's migration applied to it
cleanly. Committed to `master` as `7588358`.

## Done in the previous session, 2026-09-02 (`2e96d28`)

Per-entry billing-period choice (FDD: "When an entry is invoiced the user can choose to add it to the current
billing period or to the next billing period") — this was open item 2 from the previous session's list below.

- New `BillingPeriodChoice` enum on `TimesheetEntry` (`Current` default / `Next`), settable on Create/Update from
  the Add/Edit Entry page (a new "Bill In" dropdown, shown only for Time & Materials projects — a Fixed Fee
  project's invoiced amount doesn't derive from entries at all, so the picker would be inert there). Carried
  over as-is on Duplicate, like every other non-approval field. New migration
  `AddTimesheetEntryBillingPeriodChoice` (nullable-free, defaults existing rows to `Current`).
- New `ITimesheetEntryRepository.GetCountedForInvoicingAsync`, used only by `InvoiceGenerationService.
  BuildDraftAsync` (T&M line items) — NOT by `GetCountedForProjectAsync`, which `ProjectHealthAssessor` still
  uses unmodified, since project-health tracking isn't about billing periods. Semantics: an entry dated inside
  the period being invoiced counts normally unless it's `Next`-flagged, in which case it's excluded from *that*
  period and instead counted in the period immediately following it (same length, ending the day before the
  next period starts) — and only that one, so a deferred entry can't be swept up indefinitely by every
  subsequent invoice run if generation is skipped for a while. This is a deliberate, narrower reading of "next
  period" than "whenever the next draft happens to run" — see the "not yet decided" note below.
- 5 new unit tests (`InvoiceGenerationServiceTests`) covering: normal same-period counting, a `Next`-flagged
  entry excluded from its own period, a deferred entry correctly picked up by the immediately-following period,
  a deferred entry from *two* periods back correctly NOT picked up (proves it doesn't linger), and a
  `Current`-flagged entry from a previous period correctly NOT pulled forward.
- Backend builds clean, 36/36 tests pass (31 pre-existing + 5 added). Angular builds clean.

**Now live-verified in the browser by the user** (Claude in Chrome was declined this session, and there's no
known local-account password to script the API via curl either, so the user ran the manual pass themselves
rather than this session doing it): confirmed both that the "Bill In" picker shows for a Time & Materials
project's entries and is hidden for a Fixed Fee project's, and that setting an entry to "Next billing period"
correctly excludes it from that period's Draft invoice and correctly includes it on the following period's.
Also backed by 5 targeted unit tests directly against the repository query's period-boundary logic (exercised
through `InvoiceGenerationService.BuildDraftAsync`, not mocked), plus clean backend/Angular builds.

Dev servers left running at the end of this session, same as last time: API on `http://localhost:7071`
(`func start`), Angular on `http://localhost:3000` (`npm start`), and Azurite (started fresh this session with
the same command as before — see below). Same dev SQLite file; this session's migration applied to it cleanly.

## One thing worth deciding before treating this as fully done

- **"Next period" is defined as "the very next invoicing run for this project's client, same length, no more"**
  — not "whenever a draft next happens to be generated" and not tied to `Client.CurrentPeriodStart/End`
  directly (those only exist for `Monthly` clients; T&M invoicing can also be triggered manually with arbitrary
  date ranges for `OneOff` clients). If an admin skips generating an invoice for a period entirely, a
  `Next`-flagged entry from the skipped period is **not** swept up by whichever period eventually gets
  generated after that — it would need to be manually re-flagged. This was the simplest well-defined reading
  that avoids needing a whole "mark entry as consumed once actually invoiced" locking mechanism (which the FDD's
  "Finalizing an invoice locks the entries it was built from" line implies exists somewhere, but doesn't
  currently — a separate, pre-existing gap, not touched this session). Flag if you'd rather have the "sweep up
  whenever" behavior instead; it's a bigger change (needs the locking mechanism to avoid double-counting).

## Done in the previous session (commits, newest first)

- `cf03094` — Monthly payroll aggregation and billing roll-forward timers: the two remaining
  timer-triggered Functions the FDD calls for (only `DailyTimesheetReminder`/
  `NightlyProjectHealthAssessment` existed before). `MonthlyPayrollAggregation` (03:30 UTC on the
  1st) aggregates out-of-hours pay per staff member into new `PayrollPeriod`/`PayrollPeriodLine`
  entities from `OutOfHoursHours * ResolvedOutOfHoursCost` — deliberately not
  `TimesheetEntry.ToPayroll`, which is priced at the client-facing rate and blends regular + OOH
  hours despite its name — then notifies admins; it never auto-marks entries `SentToPayroll`, that
  stays a manual action via the existing Approvals screen. `MonthlyBillingRollForward` (03:00 UTC
  on the 1st) adds `Client.BillingPeriod` (OneOff/Monthly) + `CurrentPeriodStart`/`CurrentPeriodEnd`
  (editable via the existing Clients admin page); for every Monthly client whose period has
  closed, it generates a Draft invoice via the existing `GenerateDraftInvoiceAsync` and advances
  the window — an admin still reviews/finalizes manually. 13 new unit tests. **Now fully
  live-verified end-to-end** — see the Azurite note directly below (not yet true at the time of
  this commit itself; the verification happened in a later round of the same session).
- `1f91f41` — Fixed a real (pre-existing, not introduced this session) frontend bug found while
  live-testing item 1 above: the Approvals page didn't refresh the "Ready for Payroll" tab's
  count/rows after approving entries in the "Pending" tab, if the Ready tab had already been
  visited earlier in the session — it just went stale until a manual page reload.
- `f906359` — `PayrollPeriod` read UI: a new Admin-only `/admin/payroll-periods` page listing
  generated periods (month, staff count, total OOH hours/pay, generated-at) with a per-staff
  breakdown on selecting one. Closes the "no read UI this session" scope decision from the
  `cf03094` payroll-aggregation work below — that decision held for the length of one commit, not
  the whole session. Admin-only throughout (no PM variant) since `PayrollPeriod` has no project
  dimension to scope a PM against. No new tests (flat query/DTO mapping, no branching).
- `00f3a94` — Payroll Periods page now shows a `banner-error` on a failed load instead of silently
  looking empty — found via the exact 404 described two bullets down.
- `ce61218` — Manual "Run Now" triggers for both monthly timers, so an Admin can demo them without
  waiting for the 1st of the month or asking a developer to hit the Functions admin API by hand.
  Two separate buttons with `title` tooltips: "Run Now" on Payroll Periods
  (`PayrollPeriods_RunNow` → `IPayrollAggregationService.AggregateAsync`), "Run Billing
  Roll-Forward Now" on Invoicing (`BillingRollForward_RunNow` → `IBillingRollForwardService
  .RunAsync`). Each calls the exact same service method its scheduled timer calls, so behavior is
  identical either way. Both Admin-only.

**Local Azurite storage emulator installed** (no app code change, not a commit): the two new timer
Functions' *listeners* couldn't even start locally — `local.settings.json` already had the correct
`AzureWebJobsStorage=UseDevelopmentStorage=true`, but nothing was listening on Azurite's default
ports, so the func host logged `The listener for function '...' was unable to start` for all four
timers (the two new ones and the two pre-existing ones — this was never new-code-specific).
Installed globally via `npm install -g azurite`, given a persistent data directory at
`C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\azurite\` (same convention as the dev SQLite DB
and Attachments folder), and started with:
```
azurite --silent --location "C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\azurite" --debug "C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\azurite\debug.log"
```
**Azurite must be running before `func start`** for any timer-triggered Function to actually fire
(scheduled or via `POST /admin/functions/{name}`) — otherwise you'll see 202-with-no-effect or the
listener-startup error above. With it running, both new timers were manually triggered and
confirmed to have real effects (see the `cf03094` entry above and the loose-ends note that used to
flag this as unverified — now resolved).
- `5a79f83` — Project-manager scoping on payroll approvals: the approve/send-to-payroll workflow
  (`TimesheetApprovalFunctions`) was Admin-only; a PM can now additionally see/approve/send
  entries on projects they manage (FDD: "out of hours work must be approved by project managers or
  administrators"), enforced server-side (a batch mixing in an unmanaged project's entry is
  rejected whole, not partially applied). `/api/me` now reports `IsProjectManager`. **Verified
  live** against the running dev API — see commit message for the exact scenarios.

Prior session's commits (`2f2c021` and earlier) are unchanged — see git log.

All committed to `master`. Backend builds clean, 31/31 tests pass (18 pre-existing + 13 added this
session — see `PayrollAggregationServiceTests`/`BillingRollForwardServiceTests`; PM-scoping got no
new unit test, verified live instead, consistent with this codebase's existing bar for what
warrants a dedicated test).
Angular builds clean (`node node_modules/@angular/cli/bin/ng.js build`).

Local dev servers were left running this session: API on `http://localhost:7071` (`func start`
from `src/TimeSheet.Api`), Angular on `http://localhost:3000` (`npm start` from
`src/TimeSheet.Web`), **and now also Azurite** (see above — install once with
`npm install -g azurite`, then start it before `func start` every session; the func host was
restarted mid-session once Azurite came up, which is why its process/log is newer than the
Angular one). Same dev SQLite file as before
(`C:\Users\MarkLlewellyn\AppData\Local\TimeSheetDev\timesheet.db`); the new migration applied to
it cleanly on this session's `func start`.

## Scope decisions made this session (worth knowing before touching this code)

- **Payroll approval stayed scoped to all entries, not narrowed to out-of-hours-only** — confirmed
  with the user. The FDD's approval requirement is worded around out-of-hours work specifically,
  but the existing queue (inherited from the legacy Admin Overview screen) has always covered
  every entry; narrowing it would have been a bigger behavior change to an already-shipped
  workflow than this task called for. Only the *who can approve* axis changed (Admin-only → Admin
  or PM-of-that-entry's-project).
- **Payroll aggregation is monthly, aggregate-and-notify only** — both confirmed with the user. The
  FDD doesn't specify a payroll cadence (it's actually listed as an open business question in the
  FDD's own Requirements section) or say anything about auto-sending — monthly was chosen as the
  easiest-to-change-later default, and "notify only, never auto-send" mirrors how every other
  money-affecting step in this app (invoice finalization, entry approval) stays a deliberate manual
  action.
- **No read UI for `PayrollPeriod` this session** — entity + timer + notification only. The FDD's
  Payroll section only requires the aggregation to happen and be actionable via the existing
  Approvals/Send-to-Payroll screen; a dedicated list/detail page is a cheap, natural follow-up, not
  a hard requirement.
- **Billing period cadence lives on `Client`, not `Project`** — the FDD's Key Entities table and
  its stakeholder-decisions list both pair "billing period" with Client (Project's parallel field
  is "billing model", i.e. the pre-existing `PaymentModel` — a different axis); invoicing already
  generates one invoice per client across all its projects, so a client-level window avoids ever
  reconciling divergent per-project windows at generation time.
- **The FDD's per-entry "choose current vs next billing period" toggle was explicitly not built.**
  No field for it exists on `TimesheetEntry` today; adding one plus wiring it into
  `InvoiceGenerationService.BuildDraftAsync`'s date-range query is a materially larger schema+UI
  change than "add two timers" — flagged as its own follow-up, see below.

## What's still open (FDD misalignments, not yet started)

Built from a fresh, full FDD-vs-code re-audit done this session (2026-09-03) — not just a rollover
of the previous single-item list. The user asked to double-check the app against the whole FDD
document (not just areas prior sessions had already touched), on the assumption Blob Storage was
the only remaining gap. It wasn't the only one; 7 gaps found in total. 3 (all small, self-contained
fixes), project-level attachments, and reporting (entry counts + role basis) are all now done — see
the three "Done" sections above.

**One of the 7 turned out to be a false positive, corrected later the same session**: "no
rate-preview screen" was wrong — a rate preview already exists, added in an earlier session
(`bf54538`, 2026-08-28) that predates the re-audit. The consolidated Staff screen's "Project
Assignments" panel (`users-list-page.html`, driven by `ProjectAssignments_ListByUser` →
`StaffAssignmentDto.ResolvedCustomerRate`/`RateSource`) already shows, per assigned project, the
resolved rate and a label distinguishing role-default from person/role override - exactly what the
FDD asks for. The audit missed it by not checking the Staff screen specifically. No code change
needed; removed from the list rather than duplicating it.

**The FDD-numbered backlog is now fully closed.** The last remaining item was:

1. ~~**Blob Storage — bigger than originally scoped, and deliberately parked.**~~ — **fully done 2026-09-08, a
   later session**, in two rounds the same day (see both "Done" entries further up this file). First round:
   entry and project attachments moved to real Blob Storage via Azurite, reversing the "parked pending a real
   Azure Storage account" decision at the user's own request. Second round, same session: invoice PDFs too —
   `Invoice.PdfContent` (`byte[]`) replaced by `Invoice.PdfStorageKey`, same clean-cutover approach (existing
   finalized invoices' PDFs not migrated, dev/placeholder data only). Both halves of the FDD's "files are not
   stored in the database" requirement are now satisfied via the same `IFileStorageService`/
   `AzureBlobFileStorageService` abstraction and the same Azurite-backed `"attachments"` container.

**One ambiguous item, deliberately NOT on this numbered list** (flag for a decision if it ever
matters, not a bug to fix): the FDD describes a Client having two currencies — one to charge in, one
to invoice in — but `Client` has a single `CurrencyId`. A reviewer comment embedded in the FDD docx
itself ("I think you've got over hung up on currency... The whole system is otherwise GBP") suggests
this was likely a deliberate simplification already agreed with the business, not something missed.

Confirmed correct/complete by this session's audit (no action needed): 5-tier rate resolution
order, RateCard/StaffCost effective-dating, discount scope (rate-card + invoice-line only), the
Contract-entry-type-only-on-Contract-projects rule, the consolidated Staff admin screen, OOH payroll
requiring `ApprovedPayroll` before aggregation, CSV export scoping, this session's earlier
entry-locking-on-finalize work, per-entry billing-period choice, project estimated cost/profit, the
expense rechargeable flag.

(The `PayrollPeriod` read UI, the per-entry billing-period choice, and the entry-locking-on-finalize
work from earlier sessions/this session are all done — see `f906359`, the 2026-09-02 entry, and this
session's earlier entry above respectively. They are not part of the new numbered list above.)

## Known loose ends / flags already raised, not yet actioned

- ~~navigating directly to `/login` while already signed in shows both the signed-in header (nav links, "Sign
  out") *and* the login form at the same time~~ - **fixed 2026-09-10, a later session**: see that session's own
  "Done" entry above for the new `guestGuard`.
- ~~an expired session (a stale JWT in localStorage from a prior day) doesn't force a redirect to `/login` when
  an API call 401s from the Add Expense page~~ - **fixed later the same session, 2026-09-09**: see that
  session's own "Done" entry above for the new global `sessionExpiredInterceptor` - this was a real, app-wide
  gap (any page's API call 401ing would have shown the same symptom, not just this one), not something specific
  to Add Expense.
- ~~**Not resolved, mitigated further 2026-09-10**: the user reported their own logged entries missing from an
  Entry Flags search while signed in as Admin.~~ — **believed fixed 2026-09-11, a later session**, see that
  session's own "Done" entry above. Root cause found (not just re-mitigated): `SearchForFlaggingAsync` ordered
  by Date alone with no deterministic tiebreak - real dev data confirmed the take-25 cutoff routinely lands
  inside a run of same-date entries, where SQLite's own tie order (not recency) decided what survived.
  `.ThenByDescending(e => e.Id)` fixes it; live-verified the exact entry proven excluded under the old ordering
  now appears. The original report was still never reproduced under controlled conditions, so keep an eye out
  regardless.
- **EntryType rows exist on only one project** (ERP Migration Phase 2) after this session's cleanup - every
  other project in the app still has zero, so the Entry Type picker on Add Entry stays hidden for them. Adding
  more is a "which projects, what types" business decision, not something to guess at - ask the user first.
- ~~The impersonation dropdown's option list renders with invisible text~~ — **fixed 2026-09-04, same
  session.** Turned out not to be a color/CSS issue as first suspected when found - a positioning bug. The
  panel used `right-0` but its trigger sits near the left edge of the header, so the panel rendered almost
  entirely off-screen to the left; changed to `left-0` (matching the Admin dropdown's own positioning). Live-
  verified fully visible and correctly positioned afterward.
- ~~A Project Manager has no way to reach the Entry Flags screen at all~~ — **stale, already fixed.** This was
  raised when the click-through was added, but the very next round of work the same session (the nav reorg,
  `2aa6693`) fixed it as a side effect: `app.html` now shows "Entry Flags" as a flat top-level link to
  Admin-or-PM (confirmed in code, 2026-09-04), and `admin/entry-flags`'s route lost `adminGuard` (kept
  `authGuard` only, per `app.routes.ts`'s own comment there). Left this line struck through rather than deleted
  so a future read of this file doesn't wonder whether it was ever addressed. ~~The one still-real remaining gap
  in this area is narrower: the Log Time grid's flag-indicator click-through to `/admin/entry-flags?flagId=...`
  is still Admin-only~~ — **fixed 2026-09-07**: `log-time-page.ts`'s `canOpenFlag()` now allows a PM too, same
  gate the page itself and its backend already use — see this session's own "Done" entry above. ~~Code-review
  verified only (Chrome wasn't connected this session) — worth a real click-through as a PM next time it is.~~
  **Live-verified 2026-09-08** as Sarah Chen (PM) — see this session's own "Done" entry above. Fully closed now.
- **SQLite/EF can't translate `ORDER BY` on a `DateTimeOffset` column — hit again this session** for
  `ProjectAttachmentRepository.GetByProjectAsync` (silently 500'd every list call; the fix is already
  applied — see the project-attachments entry above). This is a *recurring* trap in this codebase,
  not a one-off: `NotificationRepository`, `AuditLogRepository`, and now `ProjectAttachmentRepository`
  all had to work around it the same way (materialize to a list via `ToListAsync()` first, then
  `OrderBy`/`OrderByDescending` client-side in memory). **Any new repository query that orders by a
  `DateTimeOffset` column needs this same two-step pattern from the start** - it's easy to miss
  because it compiles fine and only fails at runtime.
- **`func start` does not hot-reload new Function definitions** — hit twice this session (once for
  `PayrollPeriodsFunctions`, once for the "Run Now" endpoints). Adding a brand-new `[Function(...)]`
  to a running `func start` process silently 404s on its route until the process is restarted;
  editing an *existing* Function's body seems to work without a restart, but don't trust that
  either — if a new/changed endpoint 404s or behaves stale, restart the API host first before
  assuming there's a code bug.
- ~~**The Clients admin API's new `BillingPeriod`/`CurrentPeriodStart`/`CurrentPeriodEnd` fields**
  were live-verified indirectly...~~ — **live-verified directly 2026-09-11, a later session**, see that
  session's own "Done" entry above: a real `Clients_Update` PUT, confirmed 200, with `CurrentPeriodStart`/
  `CurrentPeriodEnd` round-tripping correctly on both switching to Monthly and back to One-off.
- ~~**The batch-rejection path for PM-scoped Approve/SendToPayroll (mixing in an entry outside the
  PM's managed projects → 400) was verified by code review, not live.**~~ — **closed properly 2026-09-11, a
  later session**, see that session's own "Done" entry above: a genuine live UI attempt (as a real, freshly-
  password-reset PM) confirmed the list-scoping half, but forging the actual cross-project batch request was
  blocked by this session's own safety classifier (using the stored auth token programmatically) - closed
  instead with a new dedicated Functions-level test (the first for this codebase), confirmed to genuinely fail
  without the fix.
- ~~**Left-over demo-data artifacts from this session's live verification**~~ — **reverted 2026-09-04, a later
  session**, at the user's explicit approval (this bypassed the app's own API/business-logic layer, so it was
  flagged before doing it - see that session's own "Done" entry above for the full mechanism and a genuine
  discrepancy it caught along the way: entry #23 turned out to also be `SentToPayroll = true`, not just
  approved, which the note below originally missed).
  - ~~Entry id 23 on project 3 ("ERP Migration Phase 2") was approved for payroll by
    `sarah.chen@svgit.co.uk` (acting as that project's PM) into a posting batch literally named
    `PM-VERIFY-BATCH (verification - can be reverted by admin if desired)`.~~ Fully reverted (both the
    Approved and SentToPayroll fields) - confirmed back in the normal Approval Queue.
  - ~~Entry id 119, a 3-hour out-of-hours entry on project 3 dated 2026-08-15, description "OOH
    verification entry for MonthlyPayrollAggregation timer test (safe to delete)" — created and
    approved (posting batch `OOH-TIMER-VERIFY`) specifically to give the payroll aggregation timer
    real data to aggregate. A `PayrollPeriod` row for August 2026 (1 staff, 3.0h) now exists because
    of it.~~ Deleted; the August 2026 `PayrollPeriod` row was rebuilt via "Run Now" (not deleted - it
    also held a second, legitimate staff member's real hours) and now correctly shows 1 staff, 1.0h.
  - ~~**Everlast** (client id 4) was switched to `BillingPeriod = Monthly` with `CurrentPeriodEnd` in
    the past (done via the browser, by you) so `MonthlyBillingRollForward` had something to act on
    — it generated **Invoice #10**, a real Draft for the 2026-08-01..08-31 period, $3,800.00 USD,
    and advanced Everlast's window to September. Decide whether to leave Everlast on Monthly
    billing (it'll keep auto-generating Drafts each month once the timer actually runs on schedule)
    or switch it back to OneOff; check Admin → Invoicing → Everlast for the Draft.~~ Reverted to
    OneOff. Invoice #10 itself was left as-is (a real draft, not undone by the billing-period change).
- Everything below is unchanged from last session:
  - ~~The Roles created earlier (Director, Senior Consultant, Consultant) have **no default RateCard
    rows**~~ — **partially fixed 2026-09-04, a later session**: Consultant and Director now have a
    Default (all clients/projects) row ($120/hr, $200/hr respectively, placeholder dev-data figures).
    Senior Consultant already had one ($100/hr) - the note was inaccurate for that role specifically.
  - ~~`ProjectsAdminService.listByClient` (frontend) has a pre-existing bug using backslashes instead
    of forward slashes in its URL template~~ — **stale, checked 2026-09-04**: the current
    `listByClient` builds `` `${baseUrl}/clients/${clientId}/projects` `` with forward slashes, and a
    full `git log -p --follow` of the file never shows a backslash version at any point in its
    history. Whatever this referred to no longer exists (or never did) - not chasing further since
    there's nothing left to fix.
  - ~~`EntryFlagsFunctions.RaiseManual` UI (entry-flags-page) takes a raw Timesheet Entry Id typed in
    by hand rather than a picker.~~ **Fixed 2026-09-04, a later session** - see that session's own
    "Done" entry above for the new search-based picker (new `EntryFlags_SearchEntries` endpoint).
  - ~~No projects have `EntryType` rows yet~~ — **partially fixed 2026-09-04, a later session**: ERP
    Migration Phase 2 (project 3) now has three (Development, Testing, Documentation) - the first
    project in the app to have any. Every other project still has none.
  - Tenant directory search (`AdminUsers_SearchTenantDirectory`) will 500/error until
    `User.Read.All` is Entra-admin-consented on the `GraphAdmin` app registration.
  - ~~`AdminUsersFunctions.ResetPassword` only logs password resets via `ILogger`, not `AuditLog`~~ —
    **fixed 2026-09-04**. The dedicated `AuditLog` table this note said would be "a natural future
    enhancement" already existed (added in `20260827155728_AddEntryFlagAndAuditLog`, used by
    `EntryFlagsFunctions`/`TimesheetEntriesFunctions`/etc.) - the comment predated that migration and
    was never updated. Wired `AdminUsersFunctions` into the existing `IAuditLogService`/`IUnitOfWork`
    exactly like those other Functions classes: a `User.PasswordReset` row (entity `User`, details =
    target email + local/SSO) alongside the pre-existing `ILogger.LogWarning` call, which was left in
    place rather than removed. Backend builds clean, 44/44 tests pass (unchanged - matches this
    codebase's bar of no dedicated test for thin audit-logging wiring, same as the `EntryFlag.Raised`/
    `Cleared` precedent it copies). API host restarted for the new constructor dependencies
    (`IAuditLogService`, `IUnitOfWork`).
  - Live-verification-only leftovers in demo data from prior sessions: an inactive test project
    "Notif Threshold Test (verification - can be deleted)" (project id 9), and project 3's
    `ProjectManagerUserId` nominating `sarah.chen` (this is now load-bearing — this session's PM
    scoping tests depend on it — don't unset it without picking a replacement PM to test against).
  - At the user's own request, 2026-09-10: entries 105 and 106 on project 3 (ERP Migration Phase 2) had their
    `EntryType` set to Testing/Documentation respectively via the app's own Edit form (not a direct DB write),
    purely so the new Project Detail Breakdown page's "By Entry Type" pie had more than one visible slice for a
    demo - entry 126 already had Development set from an earlier session. Real data, not fabricated - these are
    genuine pre-existing test entries, just newly categorized. Re-saving each via the UI also recomputed
    `ToPayroll` (490.00 each) per this codebase's existing "recompute on every save" behavior - expected, not a
    side effect worth undoing.

## How to resume

Start a new session in this repo and say:

> Read HANDOFF.md and continue the FDD-alignment work from where it left off.
