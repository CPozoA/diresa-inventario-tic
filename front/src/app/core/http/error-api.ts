import { HttpErrorResponse } from '@angular/common/http';

/** Error de la API ya interpretado: lo que la interfaz necesita para decidir qué mostrar. */
export interface ErrorApi {
  /** Código HTTP (0 si no hubo respuesta: sin red o API apagada). */
  readonly estado: number;
  /** Código estable de la API (por ejemplo, "Cuenta.Bloqueada"); nulo si la respuesta no lo trae. */
  readonly codigo: string | null;
  /** Mensaje en español para mostrar al usuario. */
  readonly mensaje: string;
}

const MENSAJE_SIN_CONEXION = 'No se pudo conectar con el servidor. Revise su conexión e inténtelo de nuevo.';
const MENSAJE_INESPERADO = 'Ocurrió un error inesperado. Inténtelo de nuevo; si continúa, avise al área de TI.';
const MENSAJE_SIN_PERMISO = 'No tiene permiso para realizar esta acción.';

/**
 * Convierte la respuesta de error de la API (ProblemDetails, RFC 9457, con el campo "codigo") en un {@link ErrorApi}.
 * Los errores esperados traen un mensaje en español escrito para el usuario; los inesperados (500) nunca muestran detalles internos.
 */
export function aErrorApi(error: unknown): ErrorApi {
  if (!(error instanceof HttpErrorResponse)) {
    return { estado: -1, codigo: null, mensaje: MENSAJE_INESPERADO };
  }

  if (error.status === 0) {
    return { estado: 0, codigo: null, mensaje: MENSAJE_SIN_CONEXION };
  }

  const cuerpo = leerCuerpo(error.error);
  const codigo = typeof cuerpo?.['codigo'] === 'string' ? cuerpo['codigo'] : null;
  const titulo = typeof cuerpo?.['title'] === 'string' ? cuerpo['title'] : null;

  if (error.status >= 500) {
    return { estado: error.status, codigo, mensaje: MENSAJE_INESPERADO };
  }

  // Sin código, el título es el genérico de HTTP en inglés ("Forbidden"): se usa un texto propio.
  if (codigo === null && error.status === 403) {
    return { estado: 403, codigo: null, mensaje: MENSAJE_SIN_PERMISO };
  }

  return { estado: error.status, codigo, mensaje: codigo !== null && titulo !== null ? titulo : MENSAJE_INESPERADO };
}

function leerCuerpo(cuerpo: unknown): Record<string, unknown> | null {
  if (typeof cuerpo === 'string') {
    try {
      const json: unknown = JSON.parse(cuerpo);
      return esObjeto(json) ? json : null;
    } catch {
      return null;
    }
  }

  return esObjeto(cuerpo) ? cuerpo : null;
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null;
}
