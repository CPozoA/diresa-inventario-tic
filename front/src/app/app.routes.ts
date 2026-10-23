import { Route, Routes } from '@angular/router';

import { autenticadoGuard, invitadoGuard, permisoGuard } from './core/auth/guards';
import { Permisos } from './core/auth/permisos';
import { MENU } from './core/layout/menu';
import { RUTAS } from './core/rutas';

const titulo = (texto: string) => `${texto} · Inventario TIC`;

const cargarPendiente = () => import('./features/pendiente/pendiente.page').then((m) => m.PendientePage);

/**
 * Funcionalidades del menú que aún no tienen pantalla: cada una con el guard de su permiso.
 * Al construir una funcionalidad, su ruta pasa a cargar su propio archivo de rutas (features/<funcionalidad>).
 */
const rutasDelMenu: Route[] = MENU.flatMap((grupo) => grupo.opciones)
  .filter((opcion) => opcion.ruta !== '')
  .map((opcion) => ({
    path: opcion.ruta,
    title: titulo(opcion.texto),
    canActivate: opcion.permiso ? [permisoGuard(opcion.permiso)] : [],
    data: { titulo: opcion.texto },
    loadComponent: cargarPendiente,
  }));

export const routes: Routes = [
  {
    path: RUTAS.iniciarSesion,
    title: titulo('Iniciar sesión'),
    canActivate: [invitadoGuard],
    loadComponent: () => import('./features/cuenta/pages/iniciar-sesion.page').then((m) => m.IniciarSesionPage),
  },
  {
    path: RUTAS.olvideContrasena,
    title: titulo('Recuperar contraseña'),
    loadComponent: () => import('./features/cuenta/pages/olvide-contrasena.page').then((m) => m.OlvideContrasenaPage),
  },
  {
    // Enlace del correo: ?usuario=...&token=...
    path: RUTAS.restablecerContrasena,
    title: titulo('Restablecer contraseña'),
    loadComponent: () =>
      import('./features/cuenta/pages/restablecer-contrasena.page').then((m) => m.RestablecerContrasenaPage),
  },
  {
    path: RUTAS.cambiarContrasena,
    title: titulo('Cambiar contraseña'),
    canActivate: [autenticadoGuard],
    loadComponent: () => import('./features/cuenta/pages/cambiar-contrasena.page').then((m) => m.CambiarContrasenaPage),
  },
  {
    // Comprobación técnica de la conexión con la API (sin sesión).
    path: RUTAS.estado,
    title: titulo('Estado'),
    loadComponent: () => import('./features/inicio/estado-conexion.page').then((m) => m.EstadoConexionPage),
  },
  {
    path: '',
    canActivate: [autenticadoGuard],
    loadComponent: () => import('./core/layout/estructura.component').then((m) => m.EstructuraComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: titulo('Resumen'),
        loadComponent: () => import('./features/inicio/resumen.page').then((m) => m.ResumenPage),
      },
      {
        path: 'inventario/nuevo',
        title: titulo('Nuevo activo'),
        canActivate: [permisoGuard(Permisos.Activos.Crear)],
        data: { titulo: 'Nuevo activo' },
        loadComponent: cargarPendiente,
      },
      ...rutasDelMenu,
    ],
  },
  { path: '**', redirectTo: '' },
];
