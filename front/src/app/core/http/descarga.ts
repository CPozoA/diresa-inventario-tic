/**
 * Nombre del archivo que indica la API en Content-Disposition. Prefiere `filename*` (UTF-8, con tildes) y, si no
 * está, `filename`. Devuelve el nombre por omisión si no hay ninguno.
 */
export function nombreDesdeEncabezado(encabezado: string | null | undefined, porOmision: string): string {
  if (!encabezado) {
    return porOmision;
  }

  const extendido = /filename\*\s*=\s*(?:UTF-8|utf-8)''([^;]+)/.exec(encabezado);
  if (extendido) {
    try {
      return decodeURIComponent(extendido[1].trim());
    } catch {
      // Codificación inválida: se prueba con filename.
    }
  }

  const simple = /filename\s*=\s*("([^"]*)"|[^;]+)/.exec(encabezado);
  const nombre = (simple?.[2] ?? simple?.[1] ?? '').trim();
  return nombre || porOmision;
}

/** Hace que el navegador guarde el archivo (en el celular, lo descarga o lo abre con la app que corresponda). */
export function guardarArchivo(contenido: Blob, nombre: string): void {
  const url = URL.createObjectURL(contenido);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre;
  enlace.style.display = 'none';
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  // Se libera después: algunos navegadores todavía leen el archivo tras el clic.
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}
