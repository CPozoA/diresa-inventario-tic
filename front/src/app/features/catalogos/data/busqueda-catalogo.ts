import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { normalizarTexto } from '../../../shared/util/texto';

/**
 * Forma de comparar nombres de catálogo: sin distinguir mayúsculas, tildes ni espacios de más.
 * «  Almacén  central » y «almacen CENTRAL» se consideran el mismo nombre.
 */
export const normalizarNombre = normalizarTexto;

/** Elementos cuyo nombre contiene el texto buscado. Sin texto, devuelve la lista completa. */
export function filtrarPorNombre(elementos: readonly ElementoCatalogoDto[], texto: string): ElementoCatalogoDto[] {
  const buscado = normalizarNombre(texto);
  return buscado === '' ? [...elementos] : elementos.filter((e) => normalizarNombre(e.nombre).includes(buscado));
}

/** Elemento que ya tiene ese nombre (activo o desactivado), para no registrar repetidos. */
export function buscarMismoNombre(elementos: readonly ElementoCatalogoDto[], texto: string): ElementoCatalogoDto | undefined {
  const buscado = normalizarNombre(texto);
  return buscado === '' ? undefined : elementos.find((e) => normalizarNombre(e.nombre) === buscado);
}
