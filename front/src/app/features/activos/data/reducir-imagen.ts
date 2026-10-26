import { ImagenReducida, LADO_FOTO, LADO_MINIATURA, medidasReducidas } from './fotos';

/** Error al preparar una imagen, con el mensaje que se muestra al usuario. */
export class ErrorImagen extends Error {}

const CALIDAD_FOTO = 0.82;
const CALIDAD_MINIATURA = 0.75;
/** Tope de la API (4 MB); a 1600 px casi nunca se acerca, pero si pasa se baja la calidad. */
const TAMANO_MAXIMO = 4 * 1024 * 1024;

/** ¿Parece una imagen? Por el tipo que informa el navegador o, si viene vacío, por la extensión. */
export function pareceImagen(archivo: Pick<File, 'type' | 'name'>): boolean {
  return archivo.type ? archivo.type.startsWith('image/') : /\.(jpe?g|png|webp|gif|bmp|heic|heif|avif)$/i.test(archivo.name);
}

export function esHeic(archivo: Pick<File, 'type' | 'name'>): boolean {
  return /hei[cf]/i.test(archivo.type) || /\.hei[cf]$/i.test(archivo.name);
}

/**
 * Reduce la foto a 1600 px y prepara la miniatura de 320 px, en JPEG (D47). Al volver a dibujarla se descartan los
 * datos internos de la cámara (ubicación GPS…); el navegador ya la endereza según la orientación de la cámara.
 */
export async function reducirImagen(archivo: File): Promise<ImagenReducida> {
  if (!pareceImagen(archivo)) {
    throw new ErrorImagen('No es una imagen. Use JPG, PNG, WebP o HEIC.');
  }

  const imagen = await abrir(archivo);
  try {
    const foto = await dibujar(imagen, LADO_FOTO, CALIDAD_FOTO);
    const final = foto.size > TAMANO_MAXIMO ? await dibujar(imagen, LADO_FOTO, 0.6) : foto;
    const miniatura = await dibujar(imagen, LADO_MINIATURA, CALIDAD_MINIATURA);
    return { foto: final, miniatura, tamanoOriginal: archivo.size };
  } finally {
    if ('close' in imagen) {
      imagen.close();
    }
  }
}

async function abrir(archivo: File): Promise<ImageBitmap | HTMLImageElement> {
  try {
    if (typeof createImageBitmap === 'function') {
      return await createImageBitmap(archivo, { imageOrientation: 'from-image' });
    }
    return await abrirConImg(archivo);
  } catch {
    // Algunos navegadores abren con <img> lo que createImageBitmap rechaza (o al revés).
    try {
      return await abrirConImg(archivo);
    } catch {
      throw new ErrorImagen(
        esHeic(archivo)
          ? 'Este navegador no abre fotos HEIC. Tómela desde aquí con «Tomar foto», o use Safari o una foto JPG.'
          : 'No se pudo abrir la imagen; puede estar dañada.',
      );
    }
  }
}

async function abrirConImg(archivo: File): Promise<HTMLImageElement> {
  const url = URL.createObjectURL(archivo);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    return img;
  } finally {
    URL.revokeObjectURL(url);
  }
}

async function dibujar(imagen: ImageBitmap | HTMLImageElement, maximo: number, calidad: number): Promise<Blob> {
  const anchoOriginal = 'naturalWidth' in imagen ? imagen.naturalWidth : imagen.width;
  const altoOriginal = 'naturalHeight' in imagen ? imagen.naturalHeight : imagen.height;
  const { ancho, alto } = medidasReducidas(anchoOriginal, altoOriginal, maximo);

  const lienzo = document.createElement('canvas');
  lienzo.width = ancho;
  lienzo.height = alto;
  const contexto = lienzo.getContext('2d');
  if (!contexto) {
    throw new ErrorImagen('El navegador no pudo preparar la imagen.');
  }
  // JPEG no tiene transparencia: el fondo de un PNG transparente queda blanco, no negro.
  contexto.fillStyle = '#ffffff';
  contexto.fillRect(0, 0, ancho, alto);
  contexto.imageSmoothingQuality = 'high';
  contexto.drawImage(imagen, 0, 0, ancho, alto);

  const blob = await new Promise<Blob | null>((listo) => lienzo.toBlob(listo, 'image/jpeg', calidad));
  if (!blob) {
    throw new ErrorImagen('El navegador no pudo preparar la imagen.');
  }
  return blob;
}
