# TimeSheet — FDD Alignment Handoff (as of 2026-09-01)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done this session (commits, newest first)

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
  the window — an admin still reviews/finalizes manually. 13 new unit tests. **Not live-verified
  end-to-end** — see loose ends below for why.
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
`src/TimeSheet.Web`). Same dev SQLite file as before
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
   dev/prod before this is planned. (Carried over, unchanged from last session.)
2. **Per-entry billing-period choice** — FDD: "When an entry is invoiced the user can choose to add
   it to the current billing period or to the next billing period." No field for this exists on
   `TimesheetEntry`; this session's billing roll-forward timer only handles the *client-level*
   cadence/roll-forward, not this per-entry choice. New, smaller scope than item 1 above.
3. **`PayrollPeriod` read UI** — a list/detail view of generated payroll periods for admins; the
   aggregation itself is done (this session), just nothing shows it yet beyond the notification
   text and the underlying (unexposed) entries.

## Known loose ends / flags already raised, not yet actioned

- **The two new timer Functions were not live-exercised end-to-end this session.** The local func
  host has no Azurite/storage-emulator instance running (`AzureWebJobsStorage=UseDevelopmentStorage=true`
  with nothing listening on `127.0.0.1:10000`) — this is the exact pre-existing gap HANDOFF already
  noted against the two older timers, now also blocking `MonthlyPayrollAggregation`/
  `MonthlyBillingRollForward`'s timer *listener* from starting (confirmed via the func host's
  startup log: `The listener for function 'Functions.MonthlyPayrollAggregation' was unable to
  start`). Manually invoking via the Functions admin endpoint (`POST /admin/functions/{name}`)
  returns 202 but doesn't actually run without a working listener. Both new services
  (`PayrollAggregationService`, `BillingRollForwardService`) are instead covered by 13 unit tests
  against an in-memory SQLite DB, which is as close to "prove the logic is correct" as this
  environment allows without Azurite. Installing/running Azurite (or Azure Storage Emulator) would
  unblock live end-to-end verification of all four timers, not just the two new ones.
- **The Clients admin API's new `BillingPeriod`/`CurrentPeriodStart`/`CurrentPeriodEnd` fields were
  not live-verified against the running dev API.** `Clients_Create`/`Clients_Update` are Admin-only,
  and this session didn't have the seeded Admin's actual password (per the existing loose-end note
  below — it's this machine's own, not a seed default) to log in as Admin. Covered by the
  `ClientsFunctions.ResolveBillingPeriod` logic being straightforward and by the Angular build
  passing, but not exercised live. A future session with Admin access should flip a demo client to
  Monthly and confirm the create/update round-trip.
- **The batch-rejection path for PM-scoped Approve/SendToPayroll (mixing in an entry outside the
  PM's managed projects → 400) was verified by code review, not live.** Exercising it live would
  need a second project managed by someone else with a pending entry, which wasn't available
  without Admin access this session (see above). The list-scoping and same-project cross-staff
  approval paths (the more commonly hit cases) *were* live-verified — see the `5a79f83` commit
  message.
- **Left-over demo-data artifact from this session's live verification**: entry id 23 on project 3
  ("ERP Migration Phase 2") was approved for payroll by `sarah.chen@svgit.co.uk` (acting as that
  project's PM) into a posting batch literally named `PM-VERIFY-BATCH (verification - can be
  reverted by admin if desired)`, confirming a PM can approve a teammate's entry on their managed
  project. Clearly labelled, safe to leave or revert (there's no "un-approve" endpoint — reverting
  would need a direct DB edit).
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
