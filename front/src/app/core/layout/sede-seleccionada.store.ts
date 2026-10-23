import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../api/api';
import { listarSedes } from '../../api/fn/sedes/listar-sedes';
import { SedeDto } from '../../api/models/sede-dto';

const CLAVE_ALMACENAMIENTO = 'inventario-tic.sede-seleccionada';

interface EstadoSedeSeleccionada {
  sedes: SedeDto[];
  /** Nulo = todas las sedes. */
  sedeId: string | null;
}

/**
 * Sede con la que trabaja el superadministrador (selector del encabezado). "Todas las sedes" muestra datos
 * consolidados; elegir una filtra las pantallas a esa sede. Los usuarios de sede no lo usan: siempre ven la suya.
 * La elección se recuerda en esta pestaña del navegador.
 */
export const SedeSeleccionadaStore = signalStore(
  { providedIn: 'root' },
  withState<EstadoSedeSeleccionada>({ sedes: [], sedeId: leerGuardada() }),
  withComputed(({ sedes, sedeId }) => ({
    sede: computed(() => sedes().find((s) => s.id === sedeId()) ?? null),
  })),
  withMethods((store, api = inject(Api)) => ({
    async cargarSedes(): Promise<void> {
      const sedes = await api.invoke(listarSedes);
      const vigente = sedes.some((s) => s.id === store.sedeId()) ? store.sedeId() : null;
      patchState(store, { sedes, sedeId: vigente });
    },

    seleccionar(sedeId: string | null): void {
      patchState(store, { sedeId });
      guardar(sedeId);
    },
  })),
);

function leerGuardada(): string | null {
  try {
    return sessionStorage.getItem(CLAVE_ALMACENAMIENTO);
  } catch {
    return null;
  }
}

function guardar(sedeId: string | null): void {
  try {
    if (sedeId === null) {
      sessionStorage.removeItem(CLAVE_ALMACENAMIENTO);
    } else {
      sessionStorage.setItem(CLAVE_ALMACENAMIENTO, sedeId);
    }
  } catch {
    // Sin almacenamiento (modo privado estricto): la elección vale hasta recargar la página.
  }
}
