import { Routes } from '@angular/router';

import { permisoGuard } from '../../core/auth/guards';
import { Permisos } from '../../core/auth/permisos';

/** Inventario: lista y ficha. Registrar y editar llegan en el Paso 7C-2. */
export const ACTIVOS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/inventario.page').then((m) => m.InventarioPage) },
  {
    path: ':id',
    title: 'Ficha del activo · Inventario TIC',
    loadComponent: () => import('./pages/ficha-activo.page').then((m) => m.FichaActivoPage),
  },
  {
    path: ':id/editar',
    title: 'Editar activo · Inventario TIC',
    canActivate: [permisoGuard(Permisos.Activos.Editar)],
    data: { titulo: 'Editar activo' },
    loadComponent: () => import('../pendiente/pendiente.page').then((m) => m.PendientePage),
  },
];
