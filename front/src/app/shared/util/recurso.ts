import { Resource } from '@angular/core';

/**
 * El valor de un recurso, o `undefined` mientras carga o si falló. `value()` lanza un error cuando el recurso falló;
 * esto no, así que sirve en plantillas y `computed` donde el error ya se muestra aparte.
 */
export function valorDe<T>(recurso: Resource<T>): T | undefined {
  return recurso.hasValue() ? recurso.value() : undefined;
}
