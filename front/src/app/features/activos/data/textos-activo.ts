import { Conservacion } from '../../../api/models/conservacion';
import { Criticidad } from '../../../api/models/criticidad';
import { EstadoGarantia } from '../../../api/models/estado-garantia';
import { FichaTecnica } from '../../../api/models/ficha-tecnica';
import { GeneracionesProcesador } from '../../../api/models/generaciones-procesador';
import { SituacionRenovacion } from '../../../api/models/situacion-renovacion';
import { TipoAdquisicion } from '../../../api/models/tipo-adquisicion';
import { TipoDisco } from '../../../api/models/tipo-disco';
import { TipoRam } from '../../../api/models/tipo-ram';
import { UsoDisco } from '../../../api/models/uso-disco';
import { normalizarTexto } from '../../../shared/util/texto';

/** Color de una pastilla (clases .ok, .alerta, .mal y .neutra de styles.scss). */
export type Tono = 'ok' | 'alerta' | 'mal' | 'neutra';

/**
 * Las listas fijas viajan como texto ("ProximoARenovar"); estos son los textos que ve el usuario.
 * Se usa NonNullable porque el cliente generado incluye null en las listas que en algún DTO son opcionales.
 */
export const TEXTO_GARANTIA: Readonly<Record<NonNullable<EstadoGarantia>, string>> = {
  SinDato: 'Sin dato',
  Vigente: 'Vigente',
  Vencida: 'Vencida',
};

export const TONO_GARANTIA: Readonly<Record<NonNullable<EstadoGarantia>, Tono>> = { SinDato: 'neutra', Vigente: 'ok', Vencida: 'mal' };

export const TEXTO_RENOVACION: Readonly<Record<NonNullable<SituacionRenovacion>, string>> = {
  SinDato: 'Sin dato',
  Vigente: 'Vida útil vigente',
  ProximoARenovar: 'Próximo a renovar',
  Renovar: 'Renovar',
};

export const TONO_RENOVACION: Readonly<Record<NonNullable<SituacionRenovacion>, Tono>> = {
  SinDato: 'neutra',
  Vigente: 'ok',
  ProximoARenovar: 'alerta',
  Renovar: 'mal',
};

export const TEXTO_CONSERVACION: Readonly<Record<NonNullable<Conservacion>, string>> = {
  Nuevo: 'Nuevo',
  Bueno: 'Bueno',
  Regular: 'Regular',
  Malo: 'Malo',
};

export const TEXTO_CRITICIDAD: Readonly<Record<NonNullable<Criticidad>, string>> = { Alta: 'Alta', Media: 'Media', Baja: 'Baja' };

export const TEXTO_ADQUISICION: Readonly<Record<NonNullable<TipoAdquisicion>, string>> = {
  Compra: 'Compra',
  Arrendamiento: 'Arrendamiento',
  Donacion: 'Donación',
  Transferencia: 'Transferencia',
  Comodato: 'Comodato',
};

export const TEXTO_TIPO_DISCO: Readonly<Record<NonNullable<TipoDisco>, string>> = {
  SsdSata: 'SSD SATA',
  Nvme: 'NVMe',
  Hdd: 'HDD',
  Hibrido: 'Híbrido',
};

export const TEXTO_USO_DISCO: Readonly<Record<NonNullable<UsoDisco>, string>> = { Sistema: 'Sistema', Datos: 'Datos', Respaldo: 'Respaldo' };

export const TEXTO_TIPO_RAM: Readonly<Record<NonNullable<TipoRam>, string>> = {
  Ddr3: 'DDR3',
  Ddr3L: 'DDR3L',
  Ddr4: 'DDR4',
  Ddr5: 'DDR5',
  Lpddr4X: 'LPDDR4X',
  Lpddr5: 'LPDDR5',
};

/** Qué características técnicas lleva cada tipo de activo (D43). */
export const TEXTO_FICHA: Readonly<Record<NonNullable<FichaTecnica>, string>> = {
  Computo: 'Cómputo',
  Red: 'Red',
  Ninguna: 'Ninguna',
};

export const DESCRIPCION_FICHA: Readonly<Record<NonNullable<FichaTecnica>, string>> = {
  Computo: 'Todas las características, con discos y memoria RAM.',
  Red: 'Solo nombre de equipo, IP y MAC.',
  Ninguna: 'Sin características técnicas.',
};

/** Qué generaciones ofrece una familia de procesadores (D44). */
export const TEXTO_GENERACIONES: Readonly<Record<NonNullable<GeneracionesProcesador>, string>> = {
  IntelCore: 'Intel Core',
  IntelCoreUltra: 'Intel Core Ultra',
  AmdRyzen: 'AMD Ryzen',
  Ninguna: 'Sin generación',
};

export const DESCRIPCION_GENERACIONES: Readonly<Record<NonNullable<GeneracionesProcesador>, string>> = {
  IntelCore: '1.ª a 14.ª generación.',
  IntelCoreUltra: 'Serie 1 a Serie 3.',
  AmdRyzen: 'Serie 1000 a Serie 9000.',
  Ninguna: 'Sin lista de generaciones (Celeron, Xeon, Apple…).',
};

/** Orden en que se ofrecen las familias al elegir. */
export const FAMILIAS_GENERACIONES: readonly NonNullable<GeneracionesProcesador>[] = ['IntelCore', 'IntelCoreUltra', 'AmdRyzen', 'Ninguna'];

/**
 * Lector de una de las tablas anteriores que acepta null: según el campo, el cliente generado puede declarar la lista
 * con null (por ejemplo, la criticidad es opcional en la ficha). Sin valor devuelve el texto indicado.
 */
function lector<T extends string, R>(mapa: Readonly<Record<T, R>>, vacio: R): (valor: T | null | undefined) => R {
  return (valor) => (valor === null || valor === undefined ? vacio : (mapa[valor] ?? vacio));
}

export const textoGarantia = lector(TEXTO_GARANTIA, 'Sin dato');
export const tonoGarantia = lector<NonNullable<EstadoGarantia>, Tono>(TONO_GARANTIA, 'neutra');
export const textoRenovacion = lector(TEXTO_RENOVACION, 'Sin dato');
export const tonoRenovacion = lector<NonNullable<SituacionRenovacion>, Tono>(TONO_RENOVACION, 'neutra');
export const textoConservacion = lector(TEXTO_CONSERVACION, '—');
export const textoCriticidad = lector(TEXTO_CRITICIDAD, '—');
export const textoAdquisicion = lector(TEXTO_ADQUISICION, '—');
export const textoTipoDisco = lector(TEXTO_TIPO_DISCO, '—');
export const textoUsoDisco = lector(TEXTO_USO_DISCO, '—');
export const textoTipoRam = lector(TEXTO_TIPO_RAM, '—');
export const textoFicha = lector(TEXTO_FICHA, TEXTO_FICHA.Ninguna);
export const descripcionFicha = lector(DESCRIPCION_FICHA, DESCRIPCION_FICHA.Ninguna);
export const textoGeneraciones = lector(TEXTO_GENERACIONES, TEXTO_GENERACIONES.Ninguna);
export const descripcionGeneraciones = lector(DESCRIPCION_GENERACIONES, DESCRIPCION_GENERACIONES.Ninguna);

/** Valores de cada lista, en el orden en que se ofrecen. */
export const GARANTIAS = Object.keys(TEXTO_GARANTIA) as readonly NonNullable<EstadoGarantia>[];
export const RENOVACIONES = Object.keys(TEXTO_RENOVACION) as readonly NonNullable<SituacionRenovacion>[];

/** Orden en que se ofrecen las fichas al elegir. */
export const FICHAS: readonly NonNullable<FichaTecnica>[] = ['Computo', 'Red', 'Ninguna'];

/**
 * Color del estado del activo. Los estados son un catálogo (se pueden agregar), así que se reconocen por nombre;
 * uno desconocido se muestra neutro.
 */
export function tonoEstado(nombre: string): Tono {
  const clave = normalizarTexto(nombre);
  if (clave === 'operativo') {
    return 'ok';
  }
  if (clave.includes('mantenimiento') || clave.includes('reparacion')) {
    return 'alerta';
  }
  if (clave.includes('inoperativo') || clave.includes('baja')) {
    return 'mal';
  }
  return 'neutra';
}

/** La API envía los enteros como número o como texto (OpenAPI de .NET); aquí siempre son número. */
export function aNumero(valor: number | string | null | undefined): number | null {
  if (valor === null || valor === undefined || valor === '') {
    return null;
  }
  const numero = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(numero) ? numero : null;
}

const ENTEROS = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 0 });
const UN_DECIMAL = new Intl.NumberFormat('es-PE', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const SOLES = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN' });

/** "1,536 GB". */
export function formatoGb(valor: number | string | null | undefined): string {
  const numero = aNumero(valor);
  return numero === null ? '—' : `${ENTEROS.format(numero)} GB`;
}

/** Años con un decimal: "3.6". */
export function formatoAnios(valor: number | string | null | undefined): string {
  const numero = aNumero(valor);
  return numero === null ? '—' : UN_DECIMAL.format(numero);
}

/** "S/ 3,850.00". */
export function formatoSoles(valor: number | string | null | undefined): string {
  const numero = aNumero(valor);
  return numero === null ? '—' : SOLES.format(numero);
}

/**
 * Fecha "aaaa-mm-dd" (o un instante ISO) como "dd/mm/aaaa". Las fechas sin hora se leen tal cual, sin pasar por
 * la zona horaria del navegador (que podría mostrar el día anterior).
 */
export function formatoFecha(valor: string | null | undefined): string {
  if (!valor) {
    return '—';
  }
  const soloFecha = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valor);
  if (soloFecha) {
    return `${soloFecha[3]}/${soloFecha[2]}/${soloFecha[1]}`;
  }
  const instante = new Date(valor);
  if (Number.isNaN(instante.getTime())) {
    return '—';
  }
  const dos = (n: number) => String(n).padStart(2, '0');
  return `${dos(instante.getDate())}/${dos(instante.getMonth() + 1)}/${instante.getFullYear()}`;
}

/**
 * Fecha prevista de renovación: la de adquisición más los años de vida útil (igual que la API).
 * El 29 de febrero pasa al 28 si el año de renovación no es bisiesto.
 */
export function fechaRenovacion(
  fechaAdquisicion: string | null | undefined,
  vidaUtilAnios: number | string | null | undefined,
): string | null {
  const anios = aNumero(vidaUtilAnios);
  const partes = fechaAdquisicion ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(fechaAdquisicion) : null;
  if (!partes || anios === null) {
    return null;
  }
  const anio = Number(partes[1]) + anios;
  const mes = Number(partes[2]);
  const ultimoDia = new Date(Date.UTC(anio, mes, 0)).getUTCDate();
  const dia = Math.min(Number(partes[3]), ultimoDia);
  return `${anio}-${partes[2]}-${String(dia).padStart(2, '0')}`;
}

/** Plural sencillo: "1 disco", "2 discos". */
export function cantidad(n: number, singular: string, plural: string): string {
  return `${n} ${n === 1 ? singular : plural}`;
}
