import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';
import { normalizarTexto } from '../../../shared/util/texto';

export type FiltroEstado = 'activos' | 'inactivos' | 'todos';

/**
 * Establecimientos que coinciden con la búsqueda y el estado. Busca por código único, Id_Establecimiento, nombre,
 * categoría, microred, provincia, distrito, ubigeo y red, sin distinguir mayúsculas ni tildes.
 */
export function filtrarEstablecimientos(
  lista: readonly EstablecimientoDto[],
  texto: string,
  estado: FiltroEstado,
): EstablecimientoDto[] {
  const buscado = normalizarTexto(texto);

  return lista.filter((e) => {
    if (estado !== 'todos' && (estado === 'activos') !== e.activo) {
      return false;
    }

    if (buscado === '') {
      return true;
    }

    const campos = [
      String(e.codigoUnico),
      String(e.idEstablecimiento),
      e.nombre,
      e.categoria ?? '',
      e.microred ?? '',
      e.provincia ?? '',
      e.distrito ?? '',
      e.ubigeo ?? '',
      e.sedeNombre,
    ];
    return campos.some((c) => normalizarTexto(c).includes(buscado));
  });
}
