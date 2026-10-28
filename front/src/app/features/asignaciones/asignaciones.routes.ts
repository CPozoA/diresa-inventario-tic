import { Routes } from '@angular/router';

/** Asignaciones (D50, D51): personal de la red con sus movimientos, y la ficha de cada persona. */
export const ASIGNACIONES_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/asignaciones.page').then((m) => m.AsignacionesPage) },
  {
    path: 'personal/:id',
    title: 'Ficha de la persona · Inventario TIC',
    loadComponent: () => import('./pages/ficha-persona.page').then((m) => m.FichaPersonaPage),
  },
];
