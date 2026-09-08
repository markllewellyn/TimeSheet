# TimeSheet — FDD Alignment Handoff (as of 2026-09-08)

## Context

The FDD (`Resources/SVGIT_FDD_Timesheets_1 1 1 2.docx`) is the source of truth for how this app should behave. We've been working through a gap analysis between the FDD and the actual app, fixing the highest-impact items first.

## Done in this session, 2026-09-08 — live-verified the PM flag-indicator click-through, closing the last open flag in that area

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

**This leaves exactly one open item**, unchanged in nature across every session so far:

1. **Blob Storage — bigger than originally scoped, and deliberately parked.** Not implemented AT
   ALL, not just for invoice PDFs: timesheet AND project attachments both go to local disk
   (`LocalFileStorageService`, the only `IFileStorageService` registered in `DependencyInjection.cs`
   — now used by two features, see the project-attachments entry above), and invoice PDFs sit as a
   `byte[]` in `Invoice.PdfContent` — directly against the FDD's "files are not stored in the
   database." **No Azure Storage account exists anywhere yet** (confirmed with the user, twice) —
   parked rather than built against Azurite as originally suggested; pick this up whenever an actual
   Azure Storage account exists to build/test against, or ask the user again if they want the
   Azurite-emulator approach after all.

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

- **New, not resolved**: the user reported their own logged entries missing from an Entry Flags search while
  signed in as Admin. Could not be reproduced (tested live with two different search terms - own entries
  appeared correctly both times) and no code path was found that would exclude the searcher's own entries.
  Adding exact Entry ID search (see this session's later entry above) should route around the most likely cause
  (the picker's 25-result, most-recent-first cap pushing an older entry out) but this is a mitigation, not a
  confirmed fix - if it happens again, get the exact search term used.
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

## How to resume

Start a new session in this repo and say:

> Read HANDOFF.md and continue the FDD-alignment work from where it left off.
