import { ActivoAtencionDto } from '../../api/models/activo-atencion-dto';
import { CantidadPorAntiguedadDto } from '../../api/models/cantidad-por-antiguedad-dto';
import { CifrasRedDto } from '../../api/models/cifras-red-dto';
import { CifrasResumenDto } from '../../api/models/cifras-resumen-dto';
import { GrupoEstado } from '../../api/models/grupo-estado';
import { aNumero, textoAntiguedad } from '../activos/data/textos-activo';

/** Color de una tarjeta o barra del resumen (clases de resumen.page.scss). */
export type ColorResumen = 'azul' | 'rojo' | 'ambar' | 'naranja' | 'verde' | 'gris' | 'violeta';

/** Una de las cuatro tarjetas de arriba. Al tocarla se abre el inventario con ese filtro. */
export interface Tarjeta {
  readonly titulo: string;
  readonly valor: number;
  readonly detalle: string;
  readonly color: ColorResumen;
  readonly icono: string;
  readonly filtro: Readonly<Record<string, string>>;
}

const n = (valor: number | string | null | undefined): number => aNumero(valor) ?? 0;

/** "79%"; sin total, "—". */
export function porcentaje(parte: number, total: number): string {
  return total > 0 ? `${Math.round((parte / total) * 100)}%` : '—';
}

/** Número con separador de miles al estilo peruano (1,630). */
export function miles(valor: number): string {
  return valor.toLocaleString('es-PE');
}

export function tarjetas(c: CifrasResumenDto): Tarjeta[] {
  const activos = n(c.activos);
  const enUso = n(c.enUso);
  const garantia = n(c.garantiaVencida);
  return [
    {
      titulo: 'Activos registrados',
      valor: activos,
      detalle: activos > 0 ? `${miles(enUso)} en uso (${porcentaje(enUso, activos)})` : 'Todavía ninguno',
      color: 'azul',
      icono: 'devices',
      filtro: {},
    },
    { titulo: 'Renovar', valor: n(c.renovar), detalle: 'vida útil agotada', color: 'rojo', icono: 'autorenew', filtro: { atencion: 'Renovar' } },
    {
      titulo: 'Próximos a renovar',
      valor: n(c.proximosARenovar),
      detalle: 'en su último año de vida útil',
      color: 'ambar',
      icono: 'schedule',
      filtro: { atencion: 'ProximoARenovar' },
    },
    {
      titulo: 'Garantía vencida',
      valor: garantia,
      detalle: activos > 0 ? `${porcentaje(garantia, activos)} del inventario` : 'sin garantías vencidas',
      color: 'naranja',
      icono: 'gpp_bad',
      filtro: { atencion: 'GarantiaVencida' },
    },
  ];
}

/** Color de cada grupo de estados (D48), el mismo en la barra y en la leyenda. */
export const COLOR_GRUPO: Readonly<Record<NonNullable<GrupoEstado>, ColorResumen>> = {
  EnUso: 'verde',
  EnMantenimiento: 'ambar',
  FueraDeServicio: 'rojo',
  EnAlmacen: 'violeta',
  DeBaja: 'gris',
};

export function colorGrupo(grupo: GrupoEstado | null | undefined): ColorResumen {
  return grupo ? COLOR_GRUPO[grupo] : 'gris';
}

/** Barras de antigüedad: el ancho es relativo al rango con más activos. */
export function barrasAntiguedad(
  rangos: readonly CantidadPorAntiguedadDto[],
): { texto: string; cantidad: number; ancho: number; color: ColorResumen; atenuado: boolean }[] {
  const maximo = Math.max(1, ...rangos.map((r) => n(r.cantidad)));
  const colores: Readonly<Record<string, ColorResumen>> = { MenosDe2: 'azul', De2A4: 'azul', De4A6: 'ambar', MasDe6: 'rojo', SinFecha: 'gris' };
  return rangos.map((r) => ({
    texto: textoAntiguedad(r.rango),
    cantidad: n(r.cantidad),
    ancho: (n(r.cantidad) / maximo) * 100,
    color: colores[r.rango ?? 'SinFecha'] ?? 'gris',
    atenuado: r.rango === 'SinFecha',
  }));
}

/** Ancho relativo (0–100) de una barra respecto al valor más alto. */
export function anchoRelativo(valor: number, maximo: number): number {
  return maximo > 0 ? (valor / maximo) * 100 : 0;
}

/** Años entre dos fechas (aaaa-mm-dd), con un decimal. */
export function aniosEntre(desde: string, hasta: string): number {
  const inicio = Date.parse(`${desde}T00:00:00Z`);
  const fin = Date.parse(`${hasta}T00:00:00Z`);
  return Math.round(((fin - inicio) / (365.25 * 24 * 3600 * 1000)) * 10) / 10;
}

const decimal = (valor: number): string => valor.toLocaleString('es-PE', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** Segunda línea de un activo que requiere atención: el equipo, por qué y dónde. */
export function detalleAtencion(a: ActivoAtencionDto, hoy: string): string {
  const equipo = `${a.marca} ${a.modelo}`;
  let motivo: string;
  switch (a.motivo) {
    case 'Renovar':
      motivo =
        a.fechaAdquisicion && a.vidaUtilAnios !== null
          ? `${decimal(aniosEntre(a.fechaAdquisicion, hoy))} años de uso y ${n(a.vidaUtilAnios)} de vida útil`
          : 'vida útil agotada';
      break;
    case 'ProximoARenovar':
      motivo = a.fechaRenovacion ? `le quedan ${decimal(Math.max(0, aniosEntre(hoy, a.fechaRenovacion)))} años de vida útil` : 'último año de vida útil';
      break;
    case 'MantenimientoVencido':
      motivo = a.proximoMantenimiento ? `preventivo vencido el ${fechaCorta(a.proximoMantenimiento)}` : 'preventivo vencido';
      break;
    default:
      motivo = a.estado;
  }
  return `${equipo} · ${motivo} · ${a.establecimiento}`;
}

/** Cómo se ordena la tabla por red. */
export type OrdenRedes = 'activos' | 'renovar' | 'completas' | 'sinActivos';

export interface FilaRed {
  readonly sedeId: string;
  readonly nombre: string;
  readonly activos: number;
  readonly enUso: number;
  readonly enUsoPorcentaje: number;
  readonly renovar: number;
  readonly proximos: number;
  readonly garantia: number;
  readonly completasPorcentaje: number | null;
  readonly establecimientos: number;
  readonly sinActivos: number;
  /** La mitad o más de sus establecimientos sin activos: la red está atrasada en el registro. */
  readonly atrasada: boolean;
}

export function filaRed(r: CifrasRedDto): FilaRed {
  const activos = n(r.activos);
  const evaluadas = n(r.fichasEvaluadas);
  const establecimientos = n(r.establecimientos);
  const sinActivos = n(r.establecimientosSinActivos);
  return {
    sedeId: r.sedeId,
    nombre: r.nombre,
    activos,
    enUso: n(r.enUso),
    enUsoPorcentaje: activos > 0 ? Math.round((n(r.enUso) / activos) * 100) : 0,
    renovar: n(r.renovar),
    proximos: n(r.proximosARenovar),
    garantia: n(r.garantiaVencida),
    completasPorcentaje: evaluadas > 0 ? Math.round((n(r.fichasCompletas) / evaluadas) * 100) : null,
    establecimientos,
    sinActivos,
    atrasada: establecimientos > 0 && sinActivos / establecimientos >= 0.5,
  };
}

export function ordenarRedes(filas: readonly FilaRed[], orden: OrdenRedes): FilaRed[] {
  const clave: Readonly<Record<OrdenRedes, (f: FilaRed) => number>> = {
    activos: (f) => f.activos,
    renovar: (f) => f.renovar,
    completas: (f) => f.completasPorcentaje ?? -1,
    sinActivos: (f) => f.sinActivos,
  };
  return [...filas].sort((a, b) => clave[orden](b) - clave[orden](a) || a.nombre.localeCompare(b.nombre, 'es'));
}

/** Fila de totales de la tabla por red. */
export function totalRedes(filas: readonly FilaRed[], redes: readonly CifrasRedDto[]): FilaRed {
  const suma = (f: (x: FilaRed) => number) => filas.reduce((total, x) => total + f(x), 0);
  const evaluadas = redes.reduce((t, r) => t + n(r.fichasEvaluadas), 0);
  const completas = redes.reduce((t, r) => t + n(r.fichasCompletas), 0);
  const activos = suma((f) => f.activos);
  const establecimientos = suma((f) => f.establecimientos);
  const sinActivos = suma((f) => f.sinActivos);
  return {
    sedeId: '',
    nombre: 'Total',
    activos,
    enUso: suma((f) => f.enUso),
    enUsoPorcentaje: activos > 0 ? Math.round((suma((f) => f.enUso) / activos) * 100) : 0,
    renovar: suma((f) => f.renovar),
    proximos: suma((f) => f.proximos),
    garantia: suma((f) => f.garantia),
    completasPorcentaje: evaluadas > 0 ? Math.round((completas / evaluadas) * 100) : null,
    establecimientos,
    sinActivos,
    atrasada: false,
  };
}

/** "27/10/2026". */
export function fechaCorta(iso: string): string {
  const [anio, mes, dia] = iso.split('-');
  return `${dia}/${mes}/${anio}`;
}
