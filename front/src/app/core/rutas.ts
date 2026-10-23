/** Rutas que el núcleo de la aplicación necesita conocer (redirecciones de sesión y enlaces fijos). */
export const RUTAS = {
  iniciarSesion: 'iniciar-sesion',
  cambiarContrasena: 'cambiar-contrasena',
  olvideContrasena: 'olvide-contrasena',
  restablecerContrasena: 'restablecer-contrasena',
  estado: 'estado',
  inicio: '',
} as const;

/** Motivo que se muestra en el inicio de sesión cuando la API cerró la sesión. */
export const MOTIVO_SESION_VENCIDA = 'sesion-vencida';

/**
 * Devuelve una ruta interna segura para volver después de iniciar sesión.
 * Rechaza direcciones externas (por ejemplo "//sitio-malicioso.com") para evitar redirecciones abiertas.
 */
export function rutaInternaSegura(volver: string | null | undefined): string {
  if (!volver || !volver.startsWith('/') || volver.startsWith('//') || volver.startsWith('/\\')) {
    return '/';
  }

  return volver.startsWith(`/${RUTAS.iniciarSesion}`) ? '/' : volver;
}
