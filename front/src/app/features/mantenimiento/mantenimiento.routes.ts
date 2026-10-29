import { Routes } from '@angular/router';

/** Mantenimiento (D53): en curso, programados e historial de la red. */
export const MANTENIMIENTO_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/mantenimiento.page').then((m) => m.MantenimientoPage) },
];
