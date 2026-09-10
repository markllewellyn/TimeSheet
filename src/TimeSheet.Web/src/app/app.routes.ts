import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { authGuard } from './core/auth/auth.guard';
import { guestGuard } from './core/auth/guest.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'timesheet' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login-page/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'bootstrap-local',
    loadComponent: () => import('./features/login/bootstrap-local-page/bootstrap-local-page').then((m) => m.BootstrapLocalPage),
  },
  {
    path: 'auth/complete',
    loadComponent: () => import('./features/login/entra-complete-page/entra-complete-page').then((m) => m.EntraCompletePage),
  },
  {
    path: 'setup',
    canActivate: [authGuard],
    loadComponent: () => import('./features/setup/setup-page/setup-page').then((m) => m.SetupPage),
  },
  {
    path: 'timesheet',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/log-time-page/log-time-page').then((m) => m.LogTimePage),
  },
  {
    path: 'timesheet/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/add-entry-page/add-entry-page').then((m) => m.AddEntryPage),
  },
  {
    path: 'timesheet/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/add-entry-page/add-entry-page').then((m) => m.AddEntryPage),
  },
  {
    path: 'expenses',
    canActivate: [authGuard],
    loadComponent: () => import('./features/expenses/expenses-list-page/expenses-list-page').then((m) => m.ExpensesListPage),
  },
  {
    path: 'expenses/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/expenses/add-expense-page/add-expense-page').then((m) => m.AddExpensePage),
  },
  {
    path: 'expenses/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./features/expenses/add-expense-page/add-expense-page').then((m) => m.AddExpensePage),
  },
  {
    path: 'timesheet/my-overview',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/my-overview-page/my-overview-page').then((m) => m.MyOverviewPage),
  },
  {
    // Any signed-in user, not just admins - the endpoint itself scopes results to projects the caller
    // manages, returning an empty list for everyone else (see Invoices_ListForProjectManager).
    path: 'my-invoices',
    canActivate: [authGuard],
    loadComponent: () => import('./features/invoicing/my-invoices-page/my-invoices-page').then((m) => m.MyInvoicesPage),
  },
  {
    path: 'timesheet/calendar',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/calendar-page/calendar-page').then((m) => m.CalendarPage),
  },

  // --- Admin area (Req 1 & 2: Clients / Projects / Rates / Users / Assignments) ---
  {
    path: 'admin/clients',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/clients-list-page/clients-list-page').then((m) => m.ClientsListPage),
  },
  {
    path: 'admin/clients/new',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/client-edit-page/client-edit-page').then((m) => m.ClientEditPage),
  },
  {
    path: 'admin/clients/:id',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/client-edit-page/client-edit-page').then((m) => m.ClientEditPage),
  },
  {
    path: 'admin/projects',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/projects-all-page/projects-all-page').then((m) => m.ProjectsAllPage),
  },
  {
    path: 'admin/clients/:clientId/projects',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/projects-list-page/projects-list-page').then((m) => m.ProjectsListPage),
  },
  {
    path: 'admin/clients/:clientId/projects/new',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/project-edit-page/project-edit-page').then((m) => m.ProjectEditPage),
  },
  {
    path: 'admin/projects/:id',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/project-edit-page/project-edit-page').then((m) => m.ProjectEditPage),
  },
  {
    path: 'admin/projects/:id/assignments',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/assignments/project-assignments-page/project-assignments-page').then((m) => m.ProjectAssignmentsPage),
  },
  {
    path: 'admin/projects/:id/entry-types',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/entry-types/project-entry-types-page/project-entry-types-page').then((m) => m.ProjectEntryTypesPage),
  },
  {
    path: 'admin/users',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/users/users-list-page/users-list-page').then((m) => m.UsersListPage),
  },
  {
    path: 'admin/roles',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/roles/roles-page/roles-page').then((m) => m.RolesPage),
  },
  {
    path: 'admin/rate-cards',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/rate-cards/rate-cards-page/rate-cards-page').then((m) => m.RateCardsPage),
  },

  // --- Entry Flags, Reports, Invoicing, Project Health (Req 6, 7, 4, 8) ---
  {
    // Admin or project manager (of at least one project) - the backend (RequireAdminOrProjectManager) scopes
    // flags to the PM's own managed project(s); this page has no adminGuard so a PM sees their own scoped
    // flags rather than being blocked from the route entirely, matching admin/approvals' pattern.
    path: 'admin/entry-flags',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/entry-flags/entry-flags-page/entry-flags-page').then((m) => m.EntryFlagsPage),
  },
  {
    path: 'admin/audit-log',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/audit-log/audit-log-page/audit-log-page').then((m) => m.AuditLogPage),
  },
  {
    path: 'admin/reports',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/reports/reports-page/reports-page').then((m) => m.ReportsPage),
  },
  {
    path: 'admin/invoicing',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/invoicing/invoicing-page/invoicing-page').then((m) => m.InvoicingPage),
  },
  {
    path: 'admin/payroll-periods',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/payroll-periods/payroll-periods-page/payroll-periods-page').then((m) => m.PayrollPeriodsPage),
  },
  {
    // Admin or project manager (of at least one project) - FDD: "out of hours work must be approved
    // by project managers or administrators." The backend scopes the list/actions to the PM's own
    // managed project(s); this page has no adminGuard so a PM with nothing to manage just sees an
    // empty queue rather than being blocked from the route entirely.
    path: 'admin/approvals',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/approvals/approvals-page/approvals-page').then((m) => m.ApprovalsPage),
  },
  {
    // Any signed-in user, not just admins - FDD: export "is available... for both users and
    // administrators." The page itself hides admin-only filters (Client/Project Manager/User) for
    // a non-admin, and the API enforces the same restriction server-side regardless.
    path: 'export',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/export/export-page/export-page').then((m) => m.ExportPage),
  },
  {
    path: 'admin/settings',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/settings/settings-page/settings-page').then((m) => m.SettingsPage),
  },
];
