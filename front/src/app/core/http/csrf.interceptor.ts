import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';

import { aErrorApi } from './error-api';
import { CsrfService } from './csrf.service';

/** Cabecera donde la API espera el token (ver Inventario.Api/Seguridad/ProteccionCsrf.cs). */
export const CABECERA_CSRF = 'X-XSRF-TOKEN';

/** Código con el que la API rechaza un token ausente o vencido. */
export const CODIGO_CSRF_INVALIDO = 'Seguridad.TokenCsrfInvalido';

const METODOS_QUE_CAMBIAN = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/**
 * Agrega el token antifalsificación a toda petición a la API que cambia datos.
 * Si la API lo rechaza (por ejemplo, porque la sesión venció y el token era de esa sesión), pide uno nuevo
 * y reintenta una sola vez.
 */
export const csrfInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  if (!necesitaToken(peticion)) {
    return siguiente(peticion);
  }

  const csrf = inject(CsrfService);

  return enviarCon(csrf.obtener(), peticion, siguiente).pipe(
    catchError((error: unknown) =>
      esRechazoCsrf(error) ? enviarCon(csrf.renovar(), peticion, siguiente) : throwError(() => error),
    ),
  );
};

function necesitaToken(peticion: HttpRequest<unknown>): boolean {
  return METODOS_QUE_CAMBIAN.has(peticion.method) && peticion.url.startsWith('/api/');
}

function enviarCon(
  token: Promise<string>,
  peticion: HttpRequest<unknown>,
  siguiente: HttpHandlerFn,
): Observable<HttpEvent<unknown>> {
  return from(token).pipe(switchMap((valor) => siguiente(peticion.clone({ setHeaders: { [CABECERA_CSRF]: valor } }))));
}

function esRechazoCsrf(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 400 && aErrorApi(error).codigo === CODIGO_CSRF_INVALIDO;
}
