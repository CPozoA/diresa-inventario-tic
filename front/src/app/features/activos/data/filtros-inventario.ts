import { EstadoGarantia } from '../../../api/models/estado-garantia';
import { SituacionRenovacion } from '../../../api/models/situacion-renovacion';
import { GARANTIAS, RENOVACIONES } from './textos-activo';

/** Filtros y página de la lista del inventario. Viven en la dirección (?buscar=…&pagina=2) para volver a ellos. */
export interface FiltrosInventario {
  readonly buscar: string;
  readonly establecimientoId: string | null;
  readonly tipoActivoId: string | null;
  readonly estadoActivoId: string | null;
  readonly garantia: EstadoGarantia | null;
  readonly renovacion: SituacionRenovacion | null;
  /** Desde 1, como en la API. */
  readonly pagina: number;
  readonly tamanoPagina: number;
}

export const TAMANOS_PAGINA: readonly number[] = [25, 50, 100];

export const FILTROS_INICIALES: FiltrosInventario = {
  buscar: '',
  establecimientoId: null,
  tipoActivoId: null,
  estadoActivoId: null,
  garantia: null,
  renovacion: null,
  pagina: 1,
  tamanoPagina: 25,
};

type Parametros = Readonly<Record<string, string | undefined>>;

const texto = (valor: string | undefined): string | null => (valor && valor.trim() !== '' ? valor.trim() : null);

function deLista<T extends string>(valor: string | undefined, lista: readonly T[]): T | null {
  return lista.find((x) => x === valor) ?? null;
}

function entero(valor: string | undefined, porOmision: number, permitidos?: readonly number[]): number {
  const numero = Number(valor);
  if (!Number.isInteger(numero) || numero < 1) {
    return porOmision;
  }
  return permitidos && !permitidos.includes(numero) ? porOmision : numero;
}

/** Lee los filtros de la dirección. Lo que no se reconoce (una lista mal escrita, una página negativa) se ignora. */
export function filtrosDesdeParametros(parametros: Parametros): FiltrosInventario {
  return {
    buscar: texto(parametros['buscar']) ?? '',
    establecimientoId: texto(parametros['establecimiento']),
    tipoActivoId: texto(parametros['tipo']),
    estadoActivoId: texto(parametros['estado']),
    garantia: deLista(parametros['garantia'], GARANTIAS),
    renovacion: deLista(parametros['renovacion'], RENOVACIONES),
    pagina: entero(parametros['pagina'], FILTROS_INICIALES.pagina),
    tamanoPagina: entero(parametros['filas'], FILTROS_INICIALES.tamanoPagina, TAMANOS_PAGINA),
  };
}

/** Parámetros de la dirección para unos filtros: solo los que no están en su valor inicial. */
export function parametrosDesdeFiltros(filtros: FiltrosInventario): Record<string, string> {
  const parametros: Record<string, string> = {};
  const buscar = filtros.buscar.trim();
  if (buscar !== '') parametros['buscar'] = buscar;
  if (filtros.establecimientoId) parametros['establecimiento'] = filtros.establecimientoId;
  if (filtros.tipoActivoId) parametros['tipo'] = filtros.tipoActivoId;
  if (filtros.estadoActivoId) parametros['estado'] = filtros.estadoActivoId;
  if (filtros.garantia) parametros['garantia'] = filtros.garantia;
  if (filtros.renovacion) parametros['renovacion'] = filtros.renovacion;
  if (filtros.pagina !== FILTROS_INICIALES.pagina) parametros['pagina'] = String(filtros.pagina);
  if (filtros.tamanoPagina !== FILTROS_INICIALES.tamanoPagina) parametros['filas'] = String(filtros.tamanoPagina);
  return parametros;
}

/** Cambia un filtro y vuelve a la primera página (la página anterior podría ya no existir). */
export function cambiarFiltro<K extends Exclude<keyof FiltrosInventario, 'pagina' | 'tamanoPagina'>>(
  filtros: FiltrosInventario,
  campo: K,
  valor: FiltrosInventario[K],
): FiltrosInventario {
  return { ...filtros, [campo]: valor, pagina: 1 };
}

/** Si hay algún filtro aplicado (para ofrecer "Quitar filtros"). */
export function hayFiltros(filtros: FiltrosInventario): boolean {
  return (
    filtros.buscar.trim() !== '' ||
    filtros.establecimientoId !== null ||
    filtros.tipoActivoId !== null ||
    filtros.estadoActivoId !== null ||
    filtros.garantia !== null ||
    filtros.renovacion !== null
  );
}

/** Quita los filtros y conserva el tamaño de página elegido. */
export function sinFiltros(filtros: FiltrosInventario): FiltrosInventario {
  return { ...FILTROS_INICIALES, tamanoPagina: filtros.tamanoPagina };
}
