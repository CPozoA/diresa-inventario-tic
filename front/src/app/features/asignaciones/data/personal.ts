import { Conservacion } from '../../../api/models/conservacion';
import { ConsultaDniDto } from '../../../api/models/consulta-dni-dto';
import { TipoMovimiento } from '../../../api/models/tipo-movimiento';
import { textoConservacion } from '../../activos/data/textos-activo';

type Tono = 'ok' | 'alerta' | 'mal' | 'neutra';

/** Las dos iniciales del avatar: las de las dos primeras palabras ("JUAN PÉREZ QUISPE" → "JP"). */
export function iniciales(nombreCompleto: string): string {
  return nombreCompleto
    .split(/\s+/)
    .filter((p) => p !== '')
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase();
}

/** "Ninguno", "1 equipo", "3 equipos". */
export function textoEquipos(cantidad: number | string): string {
  const n = Number(cantidad) || 0;
  return n === 0 ? 'Ninguno' : n === 1 ? '1 equipo' : `${n} equipos`;
}

const TEXTO_MOVIMIENTO: Readonly<Record<NonNullable<TipoMovimiento>, string>> = { Entrega: 'Entrega', Devolucion: 'Devolución' };
const TONO_MOVIMIENTO: Readonly<Record<NonNullable<TipoMovimiento>, Tono>> = { Entrega: 'ok', Devolucion: 'alerta' };

export function textoMovimiento(tipo: TipoMovimiento | null | undefined): string {
  return tipo ? TEXTO_MOVIMIENTO[tipo] : '';
}

export function tonoMovimiento(tipo: TipoMovimiento | null | undefined): Tono {
  return tipo ? TONO_MOVIMIENTO[tipo] : 'neutra';
}

/** Conservación en el historial: "Bueno" mientras la tiene; "Bueno → Regular" cuando la devolvió. */
export function conservacionHistorial(entrega: Conservacion | null | undefined, devolucion: Conservacion | null | undefined): string {
  return devolucion ? `${textoConservacion(entrega)} → ${textoConservacion(devolucion)}` : textoConservacion(entrega);
}

/** Solo dígitos, hasta 8 (se puede pegar "41 234 567"). */
export function limpiarDni(texto: string): string {
  return texto.replace(/\D/g, '').slice(0, 8);
}

export function esDniValido(texto: string): boolean {
  return /^\d{8}$/.test(texto);
}

/** Como se guardan los nombres: en mayúsculas, sin espacios de más (la API hace lo mismo). */
export function enMayusculas(texto: string): string {
  return texto.replace(/\s+/g, ' ').trim().toLocaleUpperCase('es-PE');
}

/** En qué va la búsqueda del DNI al registrar a una persona. */
export type EstadoDni = 'pendiente' | 'consultando' | 'encontrado' | 'noEncontrado' | 'noDisponible' | 'registrada';

export function estadoDesdeConsulta(consulta: ConsultaDniDto): EstadoDni {
  if (consulta.registrada) {
    return 'registrada';
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

/** Los nombres se escriben a mano solo si RENIEC no encontró el DNI o no respondió (D50). */
export function seEscribenLosNombres(estado: EstadoDni): boolean {
  return estado === 'noEncontrado' || estado === 'noDisponible';
}

export function mensajeConsulta(estado: EstadoDni): string {
  return estado === 'noEncontrado'
    ? 'RENIEC no encontró ese DNI. Revise los 8 dígitos.'
    : 'No se pudo consultar RENIEC en este momento.';
}

// ---------- Filtros de la página (quedan en la dirección) ----------

export type VistaAsignaciones = 'personal' | 'movimientos';

export interface FiltrosPersonal {
  readonly vista: VistaAsignaciones;
  readonly buscar: string;
  readonly establecimientoId: string | null;
  readonly cesados: boolean;
}

export const FILTROS_PERSONAL_INICIALES: FiltrosPersonal = { vista: 'personal', buscar: '', establecimientoId: null, cesados: false };

export function filtrosPersonalDesdeParametros(p: Readonly<Record<string, string | undefined>>): FiltrosPersonal {
  return {
    vista: p['vista'] === 'movimientos' ? 'movimientos' : 'personal',
    buscar: (p['buscar'] ?? '').trim(),
    establecimientoId: p['establecimiento'] || null,
    cesados: p['cesados'] === 'si',
  };
}

export function parametrosDesdeFiltrosPersonal(f: FiltrosPersonal): Record<string, string> {
  const parametros: Record<string, string> = {};
  if (f.vista === 'movimientos') parametros['vista'] = 'movimientos';
  if (f.buscar.trim() !== '') parametros['buscar'] = f.buscar.trim();
  if (f.establecimientoId) parametros['establecimiento'] = f.establecimientoId;
  if (f.cesados) parametros['cesados'] = 'si';
  return parametros;
}

export function hayFiltrosPersonal(f: FiltrosPersonal): boolean {
  return f.buscar.trim() !== '' || f.establecimientoId !== null || f.cesados;
}
