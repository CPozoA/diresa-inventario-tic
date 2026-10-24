import { Routes } from '@angular/router';

export const AREAS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/areas.page').then((m) => m.AreasPage) },
];

export const CATALOGOS_GENERALES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/catalogos-generales.page').then((m) => m.CatalogosGeneralesPage) },
];
