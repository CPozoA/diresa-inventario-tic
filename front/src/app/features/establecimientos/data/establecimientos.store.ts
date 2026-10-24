import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../../api/api';
import { activarEstablecimiento } from '../../../api/fn/establecimientos/activar-establecimiento';
import { desactivarEstablecimiento } from '../../../api/fn/establecimientos/desactivar-establecimiento';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';

interface EstadoEstablecimientos {
  establecimientos: EstablecimientoDto[];
  /** Sede cargada (nulo = todas). */
  sedeId: string | null;
  cargando: boolean;
  error: ErrorApi | null;
}

/** Establecimientos de una red o de todas (superadministrador), incluidos los desactivados. */
export const EstablecimientosStore = signalStore(
  withState<EstadoEstablecimientos>({ establecimientos: [], sedeId: null, cargando: false, error: null }),
  withMethods((store, api = inject(Api)) => {
    async function cargar(sedeId: string | null): Promise<void> {
      patchState(store, { sedeId, cargando: true, error: null });
      try {
        const establecimientos = await api.invoke(listarEstablecimientos, {
          incluirInactivos: true,
          ...(sedeId ? { sedeId } : {}),
        });
        patchState(store, { establecimientos });
      } catch (error: unknown) {
        patchState(store, { error: aErrorApi(error) });
      } finally {
        patchState(store, { cargando: false });
      }
    }

    return {
      cargar,

      refrescar(): Promise<void> {
        return cargar(store.sedeId());
      },

      async cambiarEstado(establecimiento: EstablecimientoDto, activo: boolean): Promise<void> {
        if (activo) {
          await api.invoke(activarEstablecimiento, { id: establecimiento.id });
        } else {
          await api.invoke(desactivarEstablecimiento, { id: establecimiento.id });
        }
        await cargar(store.sedeId());
      },
    };
  }),
);
