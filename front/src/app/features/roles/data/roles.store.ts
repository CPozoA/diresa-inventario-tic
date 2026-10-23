import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../../api/api';
import { eliminarRol } from '../../../api/fn/roles/eliminar-rol';
import { listarPermisos } from '../../../api/fn/roles/listar-permisos';
import { listarRoles } from '../../../api/fn/roles/listar-roles';
import { PermisoDto } from '../../../api/models/permiso-dto';
import { RolDto } from '../../../api/models/rol-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';

interface EstadoRoles {
  roles: RolDto[];
  permisos: PermisoDto[];
  /** Sede consultada (nula para un usuario de sede: la API usa la suya). */
  sedeId: string | null;
  cargando: boolean;
  error: ErrorApi | null;
}

/** Roles de una sede y catálogo de permisos (con la marca de cuáles puede otorgar quien consulta). */
export const RolesStore = signalStore(
  withState<EstadoRoles>({ roles: [], permisos: [], sedeId: null, cargando: false, error: null }),
  withComputed(({ permisos }) => ({
    descripcionPermiso: computed(() => new Map(permisos().map((p) => [p.codigo, p.descripcion]))),
  })),
  withMethods((store, api = inject(Api)) => {
    async function cargar(sedeId: string | null): Promise<void> {
      patchState(store, { cargando: true, error: null, sedeId });
      try {
        const [roles, permisos] = await Promise.all([
          api.invoke(listarRoles, sedeId ? { sedeId } : {}),
          store.permisos().length > 0 ? Promise.resolve(store.permisos()) : api.invoke(listarPermisos),
        ]);
        patchState(store, { roles, permisos });
      } catch (error: unknown) {
        patchState(store, { roles: [], error: aErrorApi(error) });
      } finally {
        patchState(store, { cargando: false });
      }
    }

    return {
      cargar,
      recargar: () => cargar(store.sedeId()),
      limpiar: () => patchState(store, { roles: [], sedeId: null, error: null }),

      async eliminar(rol: RolDto): Promise<void> {
        await api.invoke(eliminarRol, { id: rol.id });
        await cargar(store.sedeId());
      },
    };
  }),
);
