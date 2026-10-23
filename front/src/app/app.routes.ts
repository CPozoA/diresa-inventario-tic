import { Routes } from '@angular/router';

/**
 * Rutas principales. Cada funcionalidad se carga de forma diferida (solo cuando se visita).
 * En el Paso 5B se agregan inicio de sesión, cambio de contraseña y la estructura con menú.
 */
export const routes: Routes = [
  {
    path: '',
    title: 'Inventario TIC',
    loadComponent: () => import('./features/inicio/estado-conexion.page').then((m) => m.EstadoConexionPage),
  },
  { path: '**', redirectTo: '' },
];
