import { Routes } from '@angular/router';

/** Licencias de software (D54): lista con cifras y la ficha de cada licencia. */
export const LICENCIAS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/licencias.page').then((m) => m.LicenciasPage) },
  {
    path: ':id',
    title: 'Ficha de la licencia · Inventario TIC',
    loadComponent: () => import('./pages/ficha-licencia.page').then((m) => m.FichaLicenciaPage),
  },
];
