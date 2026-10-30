import { ConsultaDniUsuarioDto } from '../../../api/models/consulta-dni-usuario-dto';

/** En qué va la búsqueda del DNI al crear un usuario o completar el de uno anterior. */
export type EstadoDniUsuario = 'pendiente' | 'consultando' | 'encontrado' | 'noEncontrado' | 'noDisponible' | 'registrado';

export function estadoDniUsuario(consulta: ConsultaDniUsuarioDto): EstadoDniUsuario {
  if (consulta.registrado) {
    return 'registrado';
  }
  switch (consulta.reniec) {
    case 'Encontrado':
      return 'encontrado';
    case 'NoEncontrado':
      return 'noEncontrado';
    default:
      return 'noDisponible';
  }
}

export function mensajeDniUsuario(estado: EstadoDniUsuario): string {
  switch (estado) {
    case 'encontrado':
      return 'Nombres y apellidos tomados de RENIEC. Puede corregirlos si hace falta.';
    case 'noEncontrado':
      return 'RENIEC no encontró ese DNI. Revise los 8 dígitos o escriba los nombres y apellidos.';
    case 'noDisponible':
      return 'No se pudo consultar RENIEC en este momento. Escriba los nombres y apellidos.';
    default:
      return '';
  }
}

/** Palabras que van en minúscula dentro de un nombre ("Rosa de la Cruz"), salvo al inicio. */
const PARTICULAS = new Set(['de', 'del', 'la', 'las', 'los', 'y', 'e']);

/**
 * RENIEC da los nombres en mayúsculas; los usuarios se muestran como nombre propio:
 * "MARÍA DE LOS ÁNGELES" → "María de los Ángeles", "D'ANGELO" → "D'Angelo", "PÉREZ-ROJAS" → "Pérez-Rojas".
 */
export function nombrePropio(texto: string): string {
  return texto
    .trim()
    .split(/\s+/)
    .filter((p) => p !== '')
    .map((palabra, i) => {
      const minuscula = palabra.toLocaleLowerCase('es-PE');
      if (i > 0 && PARTICULAS.has(minuscula)) {
        return minuscula;
      }
      // Mayúscula al inicio y después de un guion o un apóstrofo.
      return minuscula.replace(/(^|[-'’])(\p{L})/gu, (_, antes: string, letra: string) => antes + letra.toLocaleUpperCase('es-PE'));
    })
    .join(' ');
}
