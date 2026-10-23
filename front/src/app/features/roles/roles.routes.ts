import { Routes } from '@angular/router';

export const ROLES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/roles.page').then((m) => m.RolesPage) },
];
