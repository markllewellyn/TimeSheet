import { Routes } from '@angular/router';
import { MsalGuard } from '@azure/msal-angular';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'timesheet' },
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
];
