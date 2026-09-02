# TimeSheet — FDD Alignment Handoff (as of 2026-09-02)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done this session, 2026-09-02 (`2e96d28`)

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

1. **Blob Storage for invoice PDFs** — FDD wants generated invoice PDFs (and attachments) in Blob
   Storage, not the DB. **Needs your input first** on what Azure Storage setup actually exists in
   dev/prod before this is planned. (Carried over, unchanged across sessions.)
2. **Entries aren't actually locked when an invoice is finalized** — FDD: "Finalizing an invoice
   locks the entries it was built from." `InvoicingService.FinalizeInvoiceAsync` never touches the
   underlying `TimesheetEntry` rows at all today; nothing stops an already-invoiced entry from being
   edited or deleted afterward (`Update`/`Delete`/`Duplicate` in `TimesheetEntriesFunctions` only
   check `ApprovedPayroll`/`SentToPayroll`, an unrelated payroll-side lock). Noticed while building
   this session's per-entry billing-period choice — see HANDOFF's "one thing worth deciding" note
   above for how it relates.

(The `PayrollPeriod` read UI that used to be item 3 here, and the per-entry billing-period choice
that used to be item 2, are both done — see `f906359` and this session's entry above respectively.)

## Known loose ends / flags already raised, not yet actioned

- **`func start` does not hot-reload new Function definitions** — hit twice this session (once for
  `PayrollPeriodsFunctions`, once for the "Run Now" endpoints). Adding a brand-new `[Function(...)]`
  to a running `func start` process silently 404s on its route until the process is restarted;
  editing an *existing* Function's body seems to work without a restart, but don't trust that
  either — if a new/changed endpoint 404s or behaves stale, restart the API host first before
  assuming there's a code bug.
- **The Clients admin API's new `BillingPeriod`/`CurrentPeriodStart`/`CurrentPeriodEnd` fields**
  were live-verified indirectly (you set a real client to Monthly with a past period end via the
  browser, and `MonthlyBillingRollForward` picked it up correctly), but not the raw
  `Clients_Create`/`Clients_Update` request/response shape directly against the API — low risk
  given how thin `ClientsFunctions.ResolveBillingPeriod` is, and the roll-forward timer's success
  implicitly proves the save path worked.
- **The batch-rejection path for PM-scoped Approve/SendToPayroll (mixing in an entry outside the
  PM's managed projects → 400) was verified by code review, not live.** Exercising it live would
  need a second project managed by someone else with a pending entry, which wasn't available
  without full Admin access this session. The list-scoping and same-project cross-staff approval
  paths (the more commonly hit cases) *were* live-verified — see the `5a79f83` commit message.
- **Left-over demo-data artifacts from this session's live verification**:
  - Entry id 23 on project 3 ("ERP Migration Phase 2") was approved for payroll by
    `sarah.chen@svgit.co.uk` (acting as that project's PM) into a posting batch literally named
    `PM-VERIFY-BATCH (verification - can be reverted by admin if desired)`.
  - Entry id 119, a 3-hour out-of-hours entry on project 3 dated 2026-08-15, description "OOH
    verification entry for MonthlyPayrollAggregation timer test (safe to delete)" — created and
    approved (posting batch `OOH-TIMER-VERIFY`) specifically to give the payroll aggregation timer
    real data to aggregate. A `PayrollPeriod` row for August 2026 (1 staff, 3.0h) now exists because
    of it.
  - **Everlast** (client id 4) was switched to `BillingPeriod = Monthly` with `CurrentPeriodEnd` in
    the past (done via the browser, by you) so `MonthlyBillingRollForward` had something to act on
    — it generated **Invoice #10**, a real Draft for the 2026-08-01..08-31 period, $3,800.00 USD,
    and advanced Everlast's window to September. Decide whether to leave Everlast on Monthly
    billing (it'll keep auto-generating Drafts each month once the timer actually runs on schedule)
    or switch it back to OneOff; check Admin → Invoicing → Everlast for the Draft.
  - None of the above block anything — all clearly attributable and reversible, same pattern as
    prior sessions' verification leftovers below.
- Everything below is unchanged from last session:
  - The Roles created earlier (Director, Senior Consultant, Consultant) have **no default RateCard
    rows** — role-based fallback resolution will fail for any staff/client/project combo not
    already covered by a person-level override, until someone adds one via the Rate Cards admin
    page.
  - `ProjectsAdminService.listByClient` (frontend) has a pre-existing bug using backslashes instead
    of forward slashes in its URL template — noted, not fixed, out of scope.
  - `EntryFlagsFunctions.RaiseManual` UI (entry-flags-page) takes a raw Timesheet Entry Id typed in
    by hand rather than a picker.
  - No projects have `EntryType` rows yet — the "Entry Type" picker on Add Entry only appears once
    an admin adds at least one via a project's "Entry Types" page.
  - Tenant directory search (`AdminUsers_SearchTenantDirectory`) will 500/error until
    `User.Read.All` is Entra-admin-consented on the `GraphAdmin` app registration.
  - `AdminUsersFunctions.ResetPassword` only logs password resets via `ILogger`, not `AuditLog`.
  - Live-verification-only leftovers in demo data from prior sessions: an inactive test project
    "Notif Threshold Test (verification - can be deleted)" (project id 9), and project 3's
    `ProjectManagerUserId` nominating `sarah.chen` (this is now load-bearing — this session's PM
    scoping tests depend on it — don't unset it without picking a replacement PM to test against).

## How to resume

Start a new session in this repo and say:

> Read HANDOFF.md and continue the FDD-alignment work from where it left off.
