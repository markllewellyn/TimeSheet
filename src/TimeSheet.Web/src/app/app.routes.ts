import { Routes } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';
import { adminGuard } from './core/auth/admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'timesheet' },
  {
    path: 'setup',
    canActivate: [MsalGuard],
    loadComponent: () => import('./features/setup/setup-page/setup-page').then((m) => m.SetupPage),
  },
  {
    path: 'timesheet',
    canActivate: [MsalGuard],
    loadComponent: () => import('./features/timesheet/log-time-page/log-time-page').then((m) => m.LogTimePage),
  },
  {
    path: 'timesheet/new',
    canActivate: [MsalGuard],
    loadComponent: () => import('./features/timesheet/add-entry-page/add-entry-page').then((m) => m.AddEntryPage),
  },
  {
    path: 'timesheet/:id/edit',
    canActivate: [MsalGuard],
    loadComponent: () => import('./features/timesheet/add-entry-page/add-entry-page').then((m) => m.AddEntryPage),
  },
  {
    path: 'expenses/new',
    canActivate: [MsalGuard],
    loadComponent: () => import('./features/expenses/add-expense-page/add-expense-page').then((m) => m.AddExpensePage),
  },

  // --- Admin area (Req 1 & 2: Clients / Projects / Rates / Users / Assignments) ---
  {
    path: 'admin/clients',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/clients-list-page/clients-list-page').then((m) => m.ClientsListPage),
  },
  {
    path: 'admin/clients/new',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/client-edit-page/client-edit-page').then((m) => m.ClientEditPage),
  },
  {
    path: 'admin/clients/:id',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/clients/client-edit-page/client-edit-page').then((m) => m.ClientEditPage),
  },
  {
    path: 'admin/clients/:clientId/projects',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/projects-list-page/projects-list-page').then((m) => m.ProjectsListPage),
  },
  {
    path: 'admin/clients/:clientId/projects/new',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/project-edit-page/project-edit-page').then((m) => m.ProjectEditPage),
  },
  {
    path: 'admin/projects/:id',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/projects/project-edit-page/project-edit-page').then((m) => m.ProjectEditPage),
  },
  {
    path: 'admin/projects/:id/assignments',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/assignments/project-assignments-page/project-assignments-page').then((m) => m.ProjectAssignmentsPage),
  },
  {
    path: 'admin/users',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/users/users-list-page/users-list-page').then((m) => m.UsersListPage),
  },

  // --- Escalations, Reports, Invoicing, Project Health (Req 6, 7, 4, 8) ---
  {
    path: 'admin/escalations',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/escalations/escalations-page/escalations-page').then((m) => m.EscalationsPage),
  },
  {
    path: 'admin/reports',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/reports/reports-page/reports-page').then((m) => m.ReportsPage),
  },
  {
    path: 'admin/invoicing',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/invoicing/invoicing-page/invoicing-page').then((m) => m.InvoicingPage),
  },
  {
    path: 'admin/health',
    canActivate: [MsalGuard, adminGuard],
    loadComponent: () => import('./features/admin/project-health/project-health-page/project-health-page').then((m) => m.ProjectHealthPage),
  },
];
