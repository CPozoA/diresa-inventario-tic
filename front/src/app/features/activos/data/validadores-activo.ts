import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validaciones del formulario que adelantan las de la API (Domain/Activos/Activo.cs), para avisar junto al campo.
 * La API vuelve a validar todo al guardar.
 */

const vacio = (valor: unknown): boolean => valor === null || valor === undefined || String(valor).trim() === '';

/** IPv4 completa (192.168.10.45) o IPv6. */
export function esIpValida(texto: string): boolean {
  const ip = texto.trim();
  const v4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(ip);
  if (v4) {
    return v4.slice(1).every((o) => Number(o) <= 255 && (o === '0' || !o.startsWith('0')));
  }
  return ip.includes(':') && /^[0-9A-Fa-f:.]+$/.test(ip) && ip.split('::').length <= 2;
}

/** MAC con o sin separadores: A4:BB:6D:12:34:56, a4-bb-6d-12-34-56, a4bb.6d12.3456 o A4BB6D123456. */
export function esMacValida(texto: string): boolean {
  return /^[0-9A-Fa-f]{12}$/.test(texto.trim().replace(/[:.-]/g, ''));
}

export const ipValida: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  vacio(c.value) || esIpValida(String(c.value)) ? null : { ip: true };

export const macValida: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  vacio(c.value) || esMacValida(String(c.value)) ? null : { mac: true };

/** Fecha "aaaa-mm-dd" que no sea posterior a hoy. */
export function noPosteriorA(hoy: () => string): ValidatorFn {
  return (c) => (vacio(c.value) || String(c.value) <= hoy() ? null : { futura: true });
}

/** Fin de garantía no anterior a la fecha de adquisición (el campo hermano indicado). */
export function noAnteriorA(campo: string): ValidatorFn {
  return (c) => {
    const inicio = c.parent?.get(campo)?.value as string | null | undefined;
    return vacio(c.value) || vacio(inicio) || String(c.value) >= String(inicio) ? null : { anterior: true };
  };
}

/** Número entero (los campos type="number" aceptan decimales). */
export const entero: ValidatorFn = (c) => (vacio(c.value) || Number.isInteger(Number(c.value)) ? null : { entero: true });

/** Hasta cierta cantidad de decimales (montos con 2, pulgadas con 1). */
export function decimales(maximo: number): ValidatorFn {
  return (c) => {
    if (vacio(c.value)) {
      return null;
    }
    const partes = String(c.value).split('.');
    return partes.length < 2 || partes[1].length <= maximo ? null : { decimales: { maximo } };
  };
}
