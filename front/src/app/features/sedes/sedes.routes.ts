import { Routes } from '@angular/router';

export const SEDES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/sedes.page').then((m) => m.SedesPage) },
];
