import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'timesheet' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login-page/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'bootstrap-local',
    loadComponent: () => import('./features/login/bootstrap-local-page/bootstrap-local-page').then((m) => m.BootstrapLocalPage),
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
    path: 'expenses/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/expenses/add-expense-page/add-expense-page').then((m) => m.AddExpensePage),
  },
  {
    path: 'timesheet/my-overview',
    canActivate: [authGuard],
    loadComponent: () => import('./features/timesheet/my-overview-page/my-overview-page').then((m) => m.MyOverviewPage),
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

  // --- Escalations, Reports, Invoicing, Project Health (Req 6, 7, 4, 8) ---
  {
    path: 'admin/escalations',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/escalations/escalations-page/escalations-page').then((m) => m.EscalationsPage),
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
    path: 'admin/health',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/project-health/project-health-page/project-health-page').then((m) => m.ProjectHealthPage),
  },
  {
    path: 'admin/approvals',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/approvals/approvals-page/approvals-page').then((m) => m.ApprovalsPage),
  },
  {
    path: 'admin/export',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/export/export-page/export-page').then((m) => m.ExportPage),
  },
];
