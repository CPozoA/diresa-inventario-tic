import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import { aNumero } from './textos-activo';

/** Límites de las fotos (D38, D47). Los mismos que valida la API. */
export const MAXIMO_FOTOS = 6;
export const LONGITUD_DESCRIPCION_FOTO = 60;
/** Lado mayor de la foto guardada, en píxeles. */
export const LADO_FOTO = 1600;
/** Lado mayor de la miniatura de listas y tarjetas. */
export const LADO_MINIATURA = 320;

/** Dirección de la imagen de una foto (o de su miniatura). La sirve la API con la sesión del usuario. */
export function urlFoto(raiz: string, activoId: string, fotoId: string, miniatura = false): string {
  const base = `${raiz.replace(/\/$/, '')}/api/activos/${activoId}/fotos/${fotoId}/archivo`;
  return miniatura ? `${base}?miniatura=true` : base;
}

/** Medidas para que el lado mayor no pase de `maximo`, sin agrandar las imágenes pequeñas. */
export function medidasReducidas(ancho: number, alto: number, maximo: number): { ancho: number; alto: number } {
  const mayor = Math.max(ancho, alto);
  if (mayor <= maximo) {
    return { ancho, alto };
  }
  const escala = maximo / mayor;
  return { ancho: Math.max(1, Math.round(ancho * escala)), alto: Math.max(1, Math.round(alto * escala)) };
}

/** 312 KB, 4.1 MB. */
export function formatoTamano(bytes: number): string {
  if (bytes < 1024 * 1024) {
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** La principal primero y luego en el orden en que se subieron (como las entrega la API). */
export function ordenarFotos(fotos: readonly FotoActivoDto[]): FotoActivoDto[] {
  return [...fotos].sort((a, b) => Number(b.principal) - Number(a.principal) || (aNumero(a.orden) ?? 0) - (aNumero(b.orden) ?? 0));
}

/** Texto para lectores de pantalla y títulos cuando la foto no tiene descripción. */
export function nombreFoto(descripcion: string | null | undefined, posicion: number): string {
  const texto = descripcion?.trim();
  return texto ? texto : `Foto ${posicion}`;
}

// ---------- Fotos dentro del formulario (se aplican al guardar) ----------

/** Imagen ya reducida en el navegador, lista para subir. */
export interface ImagenReducida {
  readonly foto: Blob;
  readonly miniatura: Blob;
  /** Peso del archivo que eligió el usuario, para mostrar cuánto se redujo. */
  readonly tamanoOriginal: number;
}

/** Estado de una foto nueva al guardar. */
export type EstadoSubida = 'pendiente' | 'subiendo' | 'error';

/** Una foto tal como se ve en el formulario: guardada en el servidor o nueva (todavía en el navegador). */
export interface FotoEnFormulario {
  /** Identifica la foto en la pantalla (el id de la API, o uno local para las nuevas). */
  readonly clave: string;
  /** Id en la API; nulo mientras la foto no se sube. */
  readonly id: string | null;
  readonly descripcion: string;
  readonly principal: boolean;
  /** Lo que la foto tiene guardado (para saber si cambió la descripción). */
  readonly descripcionGuardada: string;
  readonly tamanoBytes: number;
  /** Solo en las nuevas. */
  readonly imagen: ImagenReducida | null;
  /** Dirección para mostrarla: la miniatura de la API, o una dirección local de la imagen nueva. */
  readonly vista: string;
  readonly estado: EstadoSubida | null;
  readonly error: string | null;
}

/** Las fotos guardadas del activo, como punto de partida del formulario. */
export function fotosParaFormulario(fotos: readonly FotoActivoDto[], vista: (foto: FotoActivoDto) => string): FotoEnFormulario[] {
  return ordenarFotos(fotos).map((f) => ({
    clave: f.id,
    id: f.id,
    descripcion: f.descripcion ?? '',
    descripcionGuardada: f.descripcion ?? '',
    principal: f.principal,
    tamanoBytes: aNumero(f.tamanoBytes) ?? 0,
    imagen: null,
    vista: vista(f),
    estado: null,
    error: null,
  }));
}

/** Agrega fotos nuevas al final, sin pasar del máximo. Si no había principal, la primera pasa a serlo. */
export function agregarFotos(lista: readonly FotoEnFormulario[], nuevas: readonly FotoEnFormulario[]): FotoEnFormulario[] {
  const resultado = [...lista, ...nuevas.slice(0, Math.max(0, MAXIMO_FOTOS - lista.length))];
  return asegurarPrincipal(resultado);
}

/** Quita una foto. Si era la principal, pasa a serlo la siguiente. */
export function quitarFoto(lista: readonly FotoEnFormulario[], clave: string): FotoEnFormulario[] {
  return asegurarPrincipal(lista.filter((f) => f.clave !== clave));
}

export function marcarPrincipal(lista: readonly FotoEnFormulario[], clave: string): FotoEnFormulario[] {
  return lista.map((f) => (f.principal === (f.clave === clave) ? f : { ...f, principal: f.clave === clave }));
}

export function describirFoto(lista: readonly FotoEnFormulario[], clave: string, descripcion: string): FotoEnFormulario[] {
  const texto = descripcion.slice(0, LONGITUD_DESCRIPCION_FOTO);
  return lista.map((f) => (f.clave === clave ? { ...f, descripcion: texto } : f));
}

function asegurarPrincipal(lista: FotoEnFormulario[]): FotoEnFormulario[] {
  if (lista.length === 0 || lista.some((f) => f.principal)) {
    return lista;
  }
  return lista.map((f, i) => (i === 0 ? { ...f, principal: true } : f));
}

/** Lo que hay que hacer en la API para que las fotos queden como en el formulario. */
export interface PlanFotos {
  /** Ids a eliminar (primero, para dejar lugar a las nuevas). */
  readonly eliminar: readonly string[];
  readonly describir: readonly { readonly id: string; readonly descripcion: string | null }[];
  /** Claves de las fotos nuevas, en orden. */
  readonly subir: readonly string[];
  /** Clave de la foto que debe quedar como principal, si cambia respecto a lo guardado. */
  readonly principal: string | null;
}

/**
 * Compara lo guardado con lo que muestra el formulario. Al subir, la API marca como principal la primera foto de un
 * activo sin fotos; al eliminar la principal, la siguiente. Por eso la principal se fija al final, solo si cambió.
 */
export function planDeFotos(guardadas: readonly FotoActivoDto[], lista: readonly FotoEnFormulario[]): PlanFotos {
  const enLista = new Set(lista.map((f) => f.id).filter((id): id is string => !!id));
  const principalGuardada = guardadas.find((f) => f.principal)?.id ?? null;
  const principal = lista.find((f) => f.principal) ?? null;

  return {
    eliminar: guardadas.filter((f) => !enLista.has(f.id)).map((f) => f.id),
    describir: lista
      .filter((f): f is FotoEnFormulario & { id: string } => !!f.id && f.descripcion.trim() !== f.descripcionGuardada.trim())
      .map((f) => ({ id: f.id, descripcion: f.descripcion.trim() || null })),
    subir: lista.filter((f) => !f.id).map((f) => f.clave),
    // Una nueva siempre se marca: la API pudo dejar como principal otra de las recién subidas.
    principal: principal && (principal.id === null || principal.id !== principalGuardada) ? principal.clave : null,
  };
}

export function planVacio(plan: PlanFotos): boolean {
  return plan.eliminar.length === 0 && plan.describir.length === 0 && plan.subir.length === 0 && plan.principal === null;
}
