import { Routes } from '@angular/router';

export const AREAS_UBICACIONES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/areas-ubicaciones.page').then((m) => m.AreasUbicacionesPage) },
];

export const CATALOGOS_GENERALES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/catalogos-generales.page').then((m) => m.CatalogosGeneralesPage) },
];
