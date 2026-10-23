import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { RUTAS } from '../rutas';
import { CodigoPermiso } from './permisos';
import { SesionStore } from './sesion.store';

/**
 * Exige sesión iniciada. Sin sesión lleva al inicio de sesión (recordando a dónde quería ir);
 * con contraseña temporal, a la pantalla de cambio de contraseña.
 */
export const autenticadoGuard: CanActivateFn = async (_ruta, estado) => {
  const sesion = inject(SesionStore);
  const router = inject(Router);

  await sesion.asegurarVerificada();

  if (!sesion.autenticado()) {
    return router.createUrlTree([`/${RUTAS.iniciarSesion}`], { queryParams: { volver: estado.url } });
  }

  if (sesion.debeCambiarContrasena() && !estado.url.startsWith(`/${RUTAS.cambiarContrasena}`)) {
    return router.createUrlTree([`/${RUTAS.cambiarContrasena}`]);
  }

  return true;
};

/** Para el inicio de sesión: si ya hay sesión, va directo al inicio. */
export const invitadoGuard: CanActivateFn = async () => {
  const sesion = inject(SesionStore);
  const router = inject(Router);

  await sesion.asegurarVerificada();
  return sesion.autenticado() ? router.createUrlTree(['/']) : true;
};

/**
 * Exige al menos uno de los permisos indicados. Se usa después de {@link autenticadoGuard}.
 * Solo evita mostrar pantallas inútiles: la API vuelve a validar cada petición.
 */
export function permisoGuard(...permisos: CodigoPermiso[]): CanActivateFn {
  return async () => {
    const sesion = inject(SesionStore);
    const router = inject(Router);

    await sesion.asegurarVerificada();
    return sesion.tieneAlguno(permisos) ? true : router.createUrlTree(['/']);
  };
}
