import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../../api/api';
import { activarUsuario } from '../../../api/fn/usuarios/activar-usuario';
import { desactivarUsuario } from '../../../api/fn/usuarios/desactivar-usuario';
import { desbloquearUsuario } from '../../../api/fn/usuarios/desbloquear-usuario';
import { listarRoles } from '../../../api/fn/roles/listar-roles';
import { listarUsuarios } from '../../../api/fn/usuarios/listar-usuarios';
import { restablecerContrasenaUsuario } from '../../../api/fn/usuarios/restablecer-contrasena-usuario';
import { RolDto } from '../../../api/models/rol-dto';
import { UsuarioDto } from '../../../api/models/usuario-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';

interface EstadoUsuarios {
  usuarios: UsuarioDto[];
  /** Roles de la sede consultada: para saber qué usuarios se pueden gestionar. Vacío con "Todas las sedes". */
  roles: RolDto[];
  /** Nula = la propia sede (usuario de sede) o todas (superadministrador). */
  sedeId: string | null;
  cargando: boolean;
  error: ErrorApi | null;
}

/** Usuarios de la sede y las acciones de gestión sobre ellos. */
export const UsuariosStore = signalStore(
  withState<EstadoUsuarios>({ usuarios: [], roles: [], sedeId: null, cargando: false, error: null }),
  withMethods((store, api = inject(Api)) => {
    async function cargar(sedeId: string | null, esSuperAdmin: boolean): Promise<void> {
      patchState(store, { cargando: true, error: null, sedeId });
      try {
        const filtro = sedeId ? { sedeId } : {};
        const necesitaRoles = !esSuperAdmin || sedeId !== null;
        const [usuarios, roles] = await Promise.all([
          api.invoke(listarUsuarios, filtro),
          necesitaRoles ? api.invoke(listarRoles, filtro) : Promise.resolve([] as RolDto[]),
        ]);
        patchState(store, { usuarios, roles });
      } catch (error: unknown) {
        patchState(store, { usuarios: [], error: aErrorApi(error) });
      } finally {
        patchState(store, { cargando: false });
      }
    }

    /** Vuelve a leer solo la lista de usuarios, sin mostrar la barra de carga. */
    async function refrescar(): Promise<void> {
      const sedeId = store.sedeId();
      const usuarios = await api.invoke(listarUsuarios, sedeId ? { sedeId } : {});
      patchState(store, { usuarios });
    }

    return {
      cargar,
      refrescar,

      async cambiarEstado(usuario: UsuarioDto, activo: boolean): Promise<void> {
        if (activo) {
          await api.invoke(activarUsuario, { id: usuario.id });
        } else {
          await api.invoke(desactivarUsuario, { id: usuario.id });
        }
        await refrescar();
      },

      async desbloquear(usuario: UsuarioDto): Promise<void> {
        await api.invoke(desbloquearUsuario, { id: usuario.id });
        await refrescar();
      },

      /** Devuelve la contraseña temporal nueva (se muestra una sola vez). */
      async restablecerContrasena(usuario: UsuarioDto): Promise<string> {
        const respuesta = await api.invoke(restablecerContrasenaUsuario, { id: usuario.id });
        await refrescar();
        return respuesta.contrasenaTemporal;
      },
    };
  }),
);
