import { Routes } from '@angular/router';

import { permisoGuard } from '../../core/auth/guards';
import { Permisos } from '../../core/auth/permisos';
import { cambiosSinGuardarGuard } from '../../shared/util/cambios-sin-guardar';

const cargarFormulario = () => import('./pages/formulario-activo.page').then((m) => m.FormularioActivoPage);

/** Inventario: lista, registro, ficha y edición. "nuevo" va antes de ":id" para que no se lea como un id. */
export const ACTIVOS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/inventario.page').then((m) => m.InventarioPage) },
  {
    path: 'nuevo',
    title: 'Nuevo activo · Inventario TIC',
    canActivate: [permisoGuard(Permisos.Activos.Crear)],
    canDeactivate: [cambiosSinGuardarGuard],
    loadComponent: cargarFormulario,
  },
  {
    path: ':id',
    title: 'Ficha del activo · Inventario TIC',
    loadComponent: () => import('./pages/ficha-activo.page').then((m) => m.FichaActivoPage),
  },
  {
    path: ':id/editar',
    title: 'Editar activo · Inventario TIC',
    canActivate: [permisoGuard(Permisos.Activos.Editar)],
    canDeactivate: [cambiosSinGuardarGuard],
    loadComponent: cargarFormulario,
  },
];
