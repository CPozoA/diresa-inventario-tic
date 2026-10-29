import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { EjecutorMantenimiento } from '../../../api/models/ejecutor-mantenimiento';
import { MantenimientoDto } from '../../../api/models/mantenimiento-dto';
import { ResultadoMantenimiento } from '../../../api/models/resultado-mantenimiento';
import { TipoMantenimiento } from '../../../api/models/tipo-mantenimiento';
import { formatoFecha, formatoSoles } from '../../activos/data/textos-activo';

type Tono = 'ok' | 'alerta' | 'mal' | 'neutra' | 'info';

export const TIPOS_MANTENIMIENTO: readonly NonNullable<TipoMantenimiento>[] = ['Preventivo', 'Correctivo'];

export function textoTipo(tipo: TipoMantenimiento | null | undefined): string {
  return tipo === 'Preventivo' ? 'Preventivo' : tipo === 'Correctivo' ? 'Correctivo' : '';
}

/** Preventivo en azul, correctivo en ámbar (como en los bosquejos). */
export function tonoTipo(tipo: TipoMantenimiento | null | undefined): Tono {
  return tipo === 'Preventivo' ? 'info' : 'alerta';
}

export function textoEjecutor(ejecutor: EjecutorMantenimiento | null | undefined): string {
  return ejecutor === 'Proveedor' ? 'Proveedor externo' : 'Personal de TI';
}

export function textoResultado(resultado: ResultadoMantenimiento | null | undefined): string {
  return resultado === 'SinReparacion' ? 'Sin reparación' : resultado === 'Operativo' ? 'Quedó operativo' : '—';
}

export function tonoResultado(resultado: ResultadoMantenimiento | null | undefined): Tono {
  return resultado === 'SinReparacion' ? 'mal' : 'ok';
}

/** Quién lo hizo, en dos partes: "JORGE QUISPE" / "Personal de TI", o la empresa / "OS-2026-118". */
export function quien(m: Pick<MantenimientoDto, 'ejecutor' | 'tecnico' | 'proveedor' | 'ordenServicio'>): { principal: string; detalle: string } {
  return m.ejecutor === 'Proveedor'
    ? { principal: m.proveedor ?? 'Proveedor', detalle: m.ordenServicio ? `Proveedor externo · ${m.ordenServicio}` : 'Proveedor externo' }
    : { principal: m.tecnico ?? 'Personal de TI', detalle: 'Personal de TI' };
}

/** "20/10/2026" o "20/10/2026 – 21/10/2026". */
export function fechasMantenimiento(m: Pick<MantenimientoDto, 'fechaInicio' | 'fechaFin'>): string {
  return !m.fechaFin || m.fechaFin === m.fechaInicio
    ? formatoFecha(m.fechaFin ?? m.fechaInicio)
    : `${formatoFecha(m.fechaInicio)} – ${formatoFecha(m.fechaFin)}`;
}

export function textoCosto(costo: number | string | null | undefined): string {
  return costo === null || costo === undefined || costo === '' ? '—' : formatoSoles(costo);
}

/** Días de "desde" a "hasta" (fechas "aaaa-mm-dd"), sin pasar por la zona horaria. */
export function diasEntre(desde: string, hasta: string): number {
  const dia = (f: string) => {
    const [a, m, d] = f.split('-').map(Number);
    return Date.UTC(a, m - 1, d) / 86_400_000;
  };
  return Math.round(dia(hasta) - dia(desde));
}

/** "hoy", "ayer", "hace 6 días". */
export function haceCuanto(fecha: string, hoy: string): string {
  const dias = diasEntre(fecha, hoy);
  return dias <= 0 ? 'hoy' : dias === 1 ? 'ayer' : `hace ${dias} días`;
}

/** Del próximo preventivo: "Vencido hace 18 días", "Vence hoy", "En 5 días". */
export function cuandoToca(proximo: string, hoy: string): { texto: string; tono: Tono } {
  const dias = diasEntre(hoy, proximo);
  if (dias < 0) {
    return { texto: dias === -1 ? 'Vencido ayer' : `Vencido hace ${-dias} días`, tono: 'mal' };
  }
  return { texto: dias === 0 ? 'Toca hoy' : dias === 1 ? 'Mañana' : `En ${dias} días`, tono: 'alerta' };
}

/** La fecha más los meses indicados; el 31 pasa al último día del mes si hace falta. */
export function sumarMeses(fecha: string, meses: number): string {
  const [a, m, d] = fecha.split('-').map(Number);
  const total = a * 12 + (m - 1) + meses;
  const anio = Math.floor(total / 12);
  const mes = (total % 12) + 1;
  const ultimo = new Date(Date.UTC(anio, mes, 0)).getUTCDate();
  return `${anio}-${String(mes).padStart(2, '0')}-${String(Math.min(d, ultimo)).padStart(2, '0')}`;
}

/** Meses que se proponen para el próximo preventivo. */
export const MESES_PREVENTIVO_POR_OMISION = 6;

/**
 * Próximo preventivo que se propone al cerrar (D53): en un preventivo, 6 meses después del término; en un correctivo,
 * el que ya tenía.
 */
export function proximoPropuesto(tipo: TipoMantenimiento | null | undefined, fechaFin: string, actual: string | null): string | null {
  return tipo === 'Preventivo' ? sumarMeses(fechaFin, MESES_PREVENTIVO_POR_OMISION) : actual;
}

/**
 * Estado que se propone al cerrar: si quedó operativo, uno del grupo "en uso" ("Operativo" primero); si no tuvo
 * reparación, uno de "fuera de servicio" ("Inoperativo" primero). Nulo si el catálogo no tiene ninguno.
 */
export function estadoPropuesto(estados: readonly ElementoCatalogoDto[], resultado: ResultadoMantenimiento | null | undefined): string | null {
  const [grupo, preferido] = resultado === 'SinReparacion' ? ['FueraDeServicio', 'Inoperativo'] : ['EnUso', 'Operativo'];
  const delGrupo = estados.filter((e) => e.activo && (e.grupo ?? 'EnUso') === grupo);
  return (delGrupo.find((e) => e.nombre === preferido) ?? delGrupo[0])?.id ?? null;
}

// ---------- Filtros de la página (quedan en la dirección) ----------

export type VistaPagina = 'curso' | 'programados' | 'historial';

export interface FiltrosMantenimiento {
  readonly vista: VistaPagina;
  readonly buscar: string;
  readonly tipo: NonNullable<TipoMantenimiento> | null;
  readonly establecimientoId: string | null;
  readonly pagina: number;
}

export const FILTROS_MANTENIMIENTO_INICIALES: FiltrosMantenimiento = {
  vista: 'curso',
  buscar: '',
  tipo: null,
  establecimientoId: null,
  pagina: 1,
};

export function filtrosMantenimientoDesdeParametros(p: Readonly<Record<string, string | undefined>>): FiltrosMantenimiento {
  const vista: VistaPagina = p['vista'] === 'programados' || p['vista'] === 'historial' ? p['vista'] : 'curso';
  const pagina = Number(p['pagina']);
  return {
    vista,
    buscar: (p['buscar'] ?? '').trim(),
    tipo: p['tipo'] === 'Preventivo' || p['tipo'] === 'Correctivo' ? p['tipo'] : null,
    establecimientoId: p['establecimiento'] || null,
    pagina: Number.isInteger(pagina) && pagina > 1 ? pagina : 1,
  };
}

export function parametrosDesdeFiltrosMantenimiento(f: FiltrosMantenimiento): Record<string, string> {
  const r: Record<string, string> = {};
  if (f.vista !== 'curso') r['vista'] = f.vista;
  if (f.buscar.trim() !== '') r['buscar'] = f.buscar.trim();
  if (f.tipo) r['tipo'] = f.tipo;
  if (f.establecimientoId) r['establecimiento'] = f.establecimientoId;
  if (f.pagina > 1) r['pagina'] = String(f.pagina);
  return r;
}
