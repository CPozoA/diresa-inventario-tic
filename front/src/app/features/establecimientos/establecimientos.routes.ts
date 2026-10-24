import { Routes } from '@angular/router';

export const ESTABLECIMIENTOS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/establecimientos.page').then((m) => m.EstablecimientosPage) },
];
