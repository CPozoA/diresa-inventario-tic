import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { SIN_REDIRECCION_401 } from './contexto-http';
import { aErrorApi } from './error-api';
import { RUTAS } from '../rutas';

/** Código con el que la API exige cambiar una contraseña temporal. */
export const CODIGO_DEBE_CAMBIAR_CONTRASENA = 'Cuenta.DebeCambiarContrasena';

/**
 * Reacciones comunes a toda la aplicación ante ciertas respuestas de la API:
 * - 401 (la sesión venció o no existe): lleva al inicio de sesión y recuerda a dónde quería ir.
 * - 403 con Cuenta.DebeCambiarContrasena: lleva a la pantalla de cambio de contraseña.
 * El error se sigue propagando para que la pantalla que hizo la petición pueda mostrar su mensaje.
 */
export const erroresInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  const router = inject(Router);

  return siguiente(peticion).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const esInicioDeSesion = peticion.url.endsWith('/api/cuenta/iniciar-sesion');

        if (error.status === 401 && !esInicioDeSesion && !peticion.context.get(SIN_REDIRECCION_401)) {
          const volver = router.url.startsWith(`/${RUTAS.iniciarSesion}`) ? undefined : router.url;
          void router.navigate([`/${RUTAS.iniciarSesion}`], { queryParams: volver ? { volver } : {} });
        } else if (error.status === 403 && aErrorApi(error).codigo === CODIGO_DEBE_CAMBIAR_CONTRASENA) {
          void router.navigate([`/${RUTAS.cambiarContrasena}`]);
        }
      }

      return throwError(() => error);
    }),
  );
};
