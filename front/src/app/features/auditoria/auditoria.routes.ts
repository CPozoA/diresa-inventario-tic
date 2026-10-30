import { Routes } from '@angular/router';

/** Registro de auditoría (D56): quién hizo qué y cuándo. */
export const AUDITORIA_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/auditoria.page').then((m) => m.AuditoriaPage) },
];
