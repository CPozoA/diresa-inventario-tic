import { HttpContext, HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../api/api';
import { cambiarContrasena } from '../../api/fn/cuenta/cambiar-contrasena';
import { cerrarSesion } from '../../api/fn/cuenta/cerrar-sesion';
import { iniciarSesion } from '../../api/fn/cuenta/iniciar-sesion';
import { obtenerSesion } from '../../api/fn/cuenta/obtener-sesion';
import { SesionDto } from '../../api/models/sesion-dto';
import { SIN_REDIRECCION_401 } from '../http/contexto-http';
import { CsrfService } from '../http/csrf.service';
import { CodigoPermiso } from './permisos';

/** "inicial": aún no se consultó la API; "verificada": ya se sabe si hay sesión o no. */
type EstadoCarga = 'inicial' | 'cargando' | 'verificada';

interface EstadoSesion {
  sesion: SesionDto | null;
  carga: EstadoCarga;
}

const estadoInicial: EstadoSesion = { sesion: null, carga: 'inicial' };

/**
 * Sesión del usuario conectado: quién es, su sede y sus permisos. Es la única fuente de verdad del frontend
 * sobre la sesión; la cookie la maneja el navegador y JavaScript no puede leerla (HttpOnly).
 */
export const SesionStore = signalStore(
  { providedIn: 'root' },
  withState(estadoInicial),
  withComputed(({ sesion }) => {
    const permisos = computed(() => new Set(sesion()?.permisos ?? []));
    return {
      autenticado: computed(() => sesion() !== null),
      esSuperAdmin: computed(() => sesion()?.esSuperAdmin === true),
      debeCambiarContrasena: computed(() => sesion()?.debeCambiarContrasena === true),
      permisos,
    };
  }),
  withMethods((store, api = inject(Api), csrf = inject(CsrfService)) => {
    let verificacion: Promise<void> | null = null;

    /** Consulta la sesión a la API. Un 401 solo significa "no ha iniciado sesión". */
    async function cargar(): Promise<void> {
      patchState(store, { carga: 'cargando' });
      try {
        const sesion = await api.invoke(obtenerSesion, undefined, new HttpContext().set(SIN_REDIRECCION_401, true));
        patchState(store, { sesion, carga: 'verificada' });
      } catch (error: unknown) {
        patchState(store, { sesion: null, carga: 'verificada' });
        if (!(error instanceof HttpErrorResponse && error.status === 401)) {
          throw error;
        }
      }
    }

    return {
      /** Verifica la sesión una sola vez (la usan los guards al entrar a cualquier ruta). */
      asegurarVerificada(): Promise<void> {
        if (store.carga() === 'verificada') {
          return Promise.resolve();
        }

        verificacion ??= cargar().finally(() => {
          verificacion = null;
        });
        return verificacion;
      },

      /** Vuelve a consultar la sesión (por ejemplo, después de cambiar la contraseña). */
      recargar: cargar,

      async iniciarSesion(usuario: string, contrasena: string): Promise<SesionDto> {
        const sesion = await api.invoke(iniciarSesion, { body: { usuario, contrasena } });
        patchState(store, { sesion, carga: 'verificada' });
        // El token antifalsificación está ligado al usuario: el de la etapa anónima ya no sirve.
        await csrf.renovar();
        return sesion;
      },

      async cerrarSesion(): Promise<void> {
        try {
          await api.invoke(cerrarSesion);
        } finally {
          patchState(store, { sesion: null, carga: 'verificada' });
          await csrf.renovar();
        }
      },

      /** Cambia la contraseña del usuario conectado y vuelve a leer la sesión (ya sin la marca de contraseña temporal). */
      async cambiarContrasena(contrasenaActual: string, contrasenaNueva: string): Promise<void> {
        await api.invoke(cambiarContrasena, { body: { contrasenaActual, contrasenaNueva } });
        await cargar();
      },

      /** Olvida la sesión sin llamar a la API (la API ya la dio por cerrada, por ejemplo al vencer). */
      limpiar(): void {
        patchState(store, { sesion: null, carga: 'verificada' });
        void csrf.renovar().catch(() => undefined);
      },

      tienePermiso(permiso: CodigoPermiso): boolean {
        return store.permisos().has(permiso);
      },

      tieneAlguno(permisos: readonly CodigoPermiso[]): boolean {
        const propios = store.permisos();
        return permisos.some((p) => propios.has(p));
      },
    };
  }),
);
