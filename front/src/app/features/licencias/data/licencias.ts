import { FiltroLicencias } from '../../../api/models/filtro-licencias';
import { VigenciaLicencia } from '../../../api/models/vigencia-licencia';
import { sumarMeses } from '../../mantenimiento/data/mantenimiento';

export type Tono = 'ok' | 'alerta' | 'mal' | 'neutra' | 'info';

/** "Vigente", "Vence en 12 días", "Vence hoy", "Venció hace 3 días", "Archivada". */
export function textoVigencia(vigencia: VigenciaLicencia | null | undefined, dias: number | string): string {
  const d = Number(dias);
  switch (vigencia) {
    case 'PorVencer':
      return d === 0 ? 'Vence hoy' : d === 1 ? 'Vence mañana' : `Vence en ${d} días`;
    case 'Vencida':
      return d === -1 ? 'Venció ayer' : `Venció hace ${-d} días`;
    case 'Archivada':
      return 'Archivada';
    default:
      return 'Vigente';
  }
}

export function tonoVigencia(vigencia: VigenciaLicencia | null | undefined): Tono {
  return vigencia === 'PorVencer' ? 'alerta' : vigencia === 'Vencida' ? 'mal' : vigencia === 'Archivada' ? 'neutra' : 'ok';
}

/** "3 de 5 puestos". */
export function textoPuestos(equipos: number | string, puestos: number | string): string {
  const p = Number(puestos);
  return `${Number(equipos)} de ${p} ${p === 1 ? 'puesto' : 'puestos'}`;
}

/** "2 libres", "Sin puestos libres", "1 equipo de más". */
export function textoLibres(equipos: number | string, puestos: number | string): string {
  const libres = Number(puestos) - Number(equipos);
  if (libres > 0) {
    return libres === 1 ? '1 libre' : `${libres} libres`;
  }
  return libres === 0 ? 'Sin puestos libres' : libres === -1 ? '1 equipo de más' : `${-libres} equipos de más`;
}

/** Porcentaje de puestos ocupados, para la barra (hasta 100). */
export function porcentajeUso(equipos: number | string, puestos: number | string): number {
  const p = Number(puestos);
  return p > 0 ? Math.min(100, Math.round((Number(equipos) / p) * 100)) : 0;
}

/** La fecha "aaaa-mm-dd" más (o menos) los días indicados, sin pasar por la zona horaria. */
export function sumarDias(fecha: string, dias: number): string {
  const [a, m, d] = fecha.split('-').map(Number);
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/** Vencimiento de una suscripción anual que empieza en <paramref name="inicio"/>: un año menos un día. */
export function unAnoDesde(inicio: string): string {
  return sumarDias(sumarMeses(inicio, 12), -1);
}

/** Al renovar se propone el periodo siguiente: desde el día después del vencimiento, por un año. */
export function periodoRenovacion(fechaFin: string): { inicio: string; fin: string } {
  const inicio = sumarDias(fechaFin, 1);
  return { inicio, fin: unAnoDesde(inicio) };
}

// ---------- Filtros de la lista (quedan en la dirección) ----------

export const OPCIONES_FILTRO: readonly { valor: NonNullable<FiltroLicencias> | null; texto: string }[] = [
  { valor: null, texto: 'Todas (sin archivadas)' },
  { valor: 'Vigentes', texto: 'Vigentes' },
  { valor: 'PorVencer', texto: 'Por vencer' },
  { valor: 'Vencidas', texto: 'Vencidas' },
  { valor: 'Excedidas', texto: 'Con más equipos que puestos' },
  { valor: 'Archivadas', texto: 'Archivadas' },
];

export interface FiltrosLicencias {
  readonly buscar: string;
  readonly filtro: NonNullable<FiltroLicencias> | null;
}

export const FILTROS_LICENCIAS_INICIALES: FiltrosLicencias = { buscar: '', filtro: null };

const FILTROS_VALIDOS = new Set<string>(OPCIONES_FILTRO.map((o) => o.valor).filter((v) => v !== null));

export function filtrosLicenciasDesdeParametros(p: Readonly<Record<string, string | undefined>>): FiltrosLicencias {
  const filtro = p['filtro'];
  return {
    buscar: (p['buscar'] ?? '').trim(),
    filtro: filtro && FILTROS_VALIDOS.has(filtro) ? (filtro as NonNullable<FiltroLicencias>) : null,
  };
}

export function parametrosDesdeFiltrosLicencias(f: FiltrosLicencias): Record<string, string> {
  const p: Record<string, string> = {};
  if (f.buscar.trim()) {
    p['buscar'] = f.buscar.trim();
  }
  if (f.filtro) {
    p['filtro'] = f.filtro;
  }
  return p;
}
