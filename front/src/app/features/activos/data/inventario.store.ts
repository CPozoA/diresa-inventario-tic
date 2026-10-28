import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

import { Api } from '../../../api/api';
import { ListarActivos$Params, listarActivos } from '../../../api/fn/activos/listar-activos';
import { ActivoResumenDto } from '../../../api/models/activo-resumen-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { FiltrosInventario } from './filtros-inventario';
import { aNumero } from './textos-activo';

interface EstadoInventario {
  activos: ActivoResumenDto[];
  /** Activos que cumplen los filtros (en todas las páginas). */
  total: number;
  cargando: boolean;
  error: ErrorApi | null;
}

/** Parámetros de la API para unos filtros: los vacíos no se envían. */
export function consultaDesdeFiltros(filtros: FiltrosInventario, sedeId: string | null): ListarActivos$Params {
  const consulta: ListarActivos$Params = { pagina: filtros.pagina, tamanoPagina: filtros.tamanoPagina };
  if (sedeId) consulta.sedeId = sedeId;
  if (filtros.buscar.trim() !== '') consulta.buscar = filtros.buscar.trim();
  if (filtros.establecimientoId) consulta.establecimientoId = filtros.establecimientoId;
  if (filtros.tipoActivoId) consulta.tipoActivoId = filtros.tipoActivoId;
  if (filtros.estadoActivoId) consulta.estadoActivoId = filtros.estadoActivoId;
  if (filtros.garantia) consulta.garantia = filtros.garantia;
  if (filtros.renovacion) consulta.renovacion = filtros.renovacion;
  if (filtros.atencion) consulta.atencion = filtros.atencion;
  if (filtros.falta) consulta.falta = filtros.falta;
  return consulta;
}

/**
 * Una página del inventario. La API filtra y pagina (puede haber miles de activos); un usuario de sede recibe
 * solo los de su red aunque envíe otra sede.
 */
export const InventarioStore = signalStore(
  withState<EstadoInventario>({ activos: [], total: 0, cargando: false, error: null }),
  withMethods((store, api = inject(Api)) => {
    // Si el usuario cambia los filtros rápido, solo cuenta la última respuesta.
    let ultimaConsulta = 0;

    return {
      async cargar(filtros: FiltrosInventario, sedeId: string | null): Promise<void> {
        const numero = ++ultimaConsulta;
        patchState(store, { cargando: true, error: null });
        try {
          const pagina = await api.invoke(listarActivos, consultaDesdeFiltros(filtros, sedeId));
          if (numero === ultimaConsulta) {
            patchState(store, { activos: pagina.activos, total: aNumero(pagina.total) ?? 0 });
          }
        } catch (error: unknown) {
          if (numero === ultimaConsulta) {
            patchState(store, { activos: [], total: 0, error: aErrorApi(error) });
          }
        } finally {
          if (numero === ultimaConsulta) {
            patchState(store, { cargando: false });
          }
        }
      },
    };
  }),
);
