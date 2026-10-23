import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../../api/api';
import { activarSede } from '../../../api/fn/sedes/activar-sede';
import { desactivarSede } from '../../../api/fn/sedes/desactivar-sede';
import { listarSedes } from '../../../api/fn/sedes/listar-sedes';
import { SedeDto } from '../../../api/models/sede-dto';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';

interface EstadoSedes {
  sedes: SedeDto[];
  cargando: boolean;
  error: ErrorApi | null;
}

/** Sedes (solo el superadministrador). Tras cada cambio actualiza también el selector de sede del encabezado. */
export const SedesStore = signalStore(
  withState<EstadoSedes>({ sedes: [], cargando: false, error: null }),
  withMethods((store, api = inject(Api), seleccion = inject(SedeSeleccionadaStore)) => {
    async function cargar(): Promise<void> {
      patchState(store, { cargando: true, error: null });
      try {
        const sedes = await api.invoke(listarSedes);
        patchState(store, { sedes: [...sedes].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')) });
      } catch (error: unknown) {
        patchState(store, { error: aErrorApi(error) });
      } finally {
        patchState(store, { cargando: false });
      }
    }

    async function despuesDeCambiar(): Promise<void> {
      await Promise.all([cargar(), seleccion.cargarSedes()]);
    }

    return {
      cargar,
      despuesDeCambiar,

      async cambiarEstado(sede: SedeDto, activa: boolean): Promise<void> {
        if (activa) {
          await api.invoke(activarSede, { id: sede.id });
        } else {
          await api.invoke(desactivarSede, { id: sede.id });
        }
        await despuesDeCambiar();
      },
    };
  }),
);
