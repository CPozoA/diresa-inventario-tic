import { AsignacionActivoDto } from '../../../api/models/asignacion-activo-dto';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { PersonaResumenDto } from '../../../api/models/persona-resumen-dto';
import { formatoFecha } from '../../activos/data/textos-activo';
import { conservacionHistorial } from './personal';

/** Persona elegida para asignarle un equipo: lo que la pantalla muestra y envía. */
export interface PersonaElegida {
  readonly id: string;
  readonly nombreCompleto: string;
  readonly dni: string;
  /** "Asistente administrativo · Administración · Hospital de Huanta". */
  readonly detalle: string;
}

export function personaElegida(p: PersonaResumenDto): PersonaElegida {
  return { id: p.id, nombreCompleto: p.nombreCompleto, dni: p.dni, detalle: detallePersona(p) };
}

/** Cargo, área y establecimiento en una línea (los que tenga). */
export function detallePersona(p: { cargo?: string | null; area: string; establecimiento: string }): string {
  return [p.cargo, p.area, p.establecimiento].filter((x) => !!x).join(' · ');
}

/**
 * Estado que se propone al devolver un equipo (D51): el primero activo del grupo "en almacén". Nulo si el catálogo no
 * tiene ninguno (la persona elige).
 */
export function estadoParaDevolucion(estados: readonly ElementoCatalogoDto[]): string | null {
  return estados.find((e) => e.activo && e.grupo === 'EnAlmacen')?.id ?? null;
}

/**
 * Fecha del último movimiento del equipo (la mayor entrega o devolución), "aaaa-mm-dd". Una entrega nueva no puede ser
 * anterior. Las fechas "aaaa-mm-dd" se comparan como texto.
 */
export function ultimoMovimiento(historial: readonly AsignacionActivoDto[]): string | null {
  let ultima: string | null = null;
  for (const a of historial) {
    for (const fecha of [a.fechaEntrega, a.fechaDevolucion]) {
      if (fecha && (ultima === null || fecha > ultima)) {
        ultima = fecha;
      }
    }
  }
  return ultima;
}

/** "Desde el 27/10/2026 · la tiene" o "15/03/2023 – 26/10/2026". */
export function periodoAsignacion(a: AsignacionActivoDto): string {
  return a.fechaDevolucion
    ? `${formatoFecha(a.fechaEntrega)} – ${formatoFecha(a.fechaDevolucion)}`
    : `Desde el ${formatoFecha(a.fechaEntrega)} · la tiene`;
}

/** "Bueno" o "Nuevo → Bueno". */
export function conservacionAsignacion(a: AsignacionActivoDto): string {
  return conservacionHistorial(a.conservacionEntrega, a.conservacionDevolucion);
}
