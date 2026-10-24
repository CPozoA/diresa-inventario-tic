/**
 * Texto para comparar en búsquedas: sin distinguir mayúsculas, tildes ni espacios de más.
 * «  Almacén  central » y «almacen CENTRAL» dan lo mismo.
 */
export function normalizarTexto(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}
