import { normalizarTexto } from '../../../shared/util/texto';

/**
 * Forma de comparar nombres de catálogo: sin distinguir mayúsculas, tildes ni espacios de más.
 * «  Almacén  central » y «almacen CENTRAL» se consideran el mismo nombre.
 */
export const normalizarNombre = normalizarTexto;

/** Elementos cuyo nombre contiene el texto buscado. Sin texto, devuelve la lista completa. */
export function filtrarPorNombre<T extends { nombre: string }>(elementos: readonly T[], texto: string): T[] {
  const buscado = normalizarNombre(texto);
  return buscado === '' ? [...elementos] : elementos.filter((e) => normalizarNombre(e.nombre).includes(buscado));
}

/** Elemento que ya tiene ese nombre (activo o desactivado), para no registrar repetidos. */
export function buscarMismoNombre<T extends { nombre: string }>(elementos: readonly T[], texto: string): T | undefined {
  const buscado = normalizarNombre(texto);
  return buscado === '' ? undefined : elementos.find((e) => normalizarNombre(e.nombre) === buscado);
}

/**
 * Elementos que coinciden con lo escrito, para elegir mientras se escribe: primero los que empiezan así, luego los
 * que lo contienen en otra parte ("core" encuentra "Intel Core i7"). Sin texto, la lista completa.
 */
export function coincidenciasAlEscribir<T extends { nombre: string }>(elementos: readonly T[], texto: string): T[] {
  const buscado = normalizarNombre(texto);
  if (buscado === '') {
    return [...elementos];
  }
  const empiezan: T[] = [];
  const contienen: T[] = [];
  for (const e of elementos) {
    const nombre = normalizarNombre(e.nombre);
    if (nombre.startsWith(buscado)) {
      empiezan.push(e);
    } else if (nombre.includes(buscado)) {
      contienen.push(e);
    }
  }
  return [...empiezan, ...contienen];
}

/**
 * Nombres parecidos a uno nuevo (uno contiene al otro, sin distinguir mayúsculas ni tildes), para avisar antes de
 * agregar algo que quizá ya existe: "Huawei" y "Huawei Technologies".
 */
export function nombresParecidos<T extends { nombre: string }>(elementos: readonly T[], texto: string): T[] {
  const buscado = normalizarNombre(texto);
  if (buscado.length < 3) {
    return [];
  }
  return elementos.filter((e) => {
    const nombre = normalizarNombre(e.nombre);
    return nombre !== buscado && (nombre.includes(buscado) || buscado.includes(nombre));
  });
}
