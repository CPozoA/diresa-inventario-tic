import { Signal, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, from, map, of, switchMap } from 'rxjs';

import { Api } from '../../../api/api';
import { SugerenciasActivo$Params, sugerenciasActivo } from '../../../api/fn/activos/sugerencias-activo';
import { CampoSugerencia } from '../../../api/models/campo-sugerencia';

/** Filtros opcionales de una sugerencia: la marca (modelo), el sistema (versión) o el establecimiento (ubicación). */
export type FiltrosSugerencia = Pick<SugerenciasActivo$Params, 'marcaId' | 'sistemaOperativoId' | 'establecimientoId'>;

/** Sugerencias de un campo escrito: se piden al escribir o al entrar al campo. */
export interface Sugeridor {
  /** Lo que está escrito en el campo. */
  buscar(texto: string | null | undefined): void;
  readonly opciones: Signal<string[]>;
}

const ESPERA_MS = 250;

/** Quita los filtros vacíos (la API no los espera). */
export function filtrosConValor(filtros: FiltrosSugerencia): FiltrosSugerencia {
  return Object.fromEntries(Object.entries(filtros).filter(([, v]) => !!v)) as FiltrosSugerencia;
}

/**
 * Sugerencias de lo ya registrado en la red para un campo escrito (D45): los más usados primero. El campo sigue
 * aceptando un valor nuevo. Se crea dentro de un componente (usa la inyección de dependencias).
 */
export function crearSugeridor(campo: NonNullable<CampoSugerencia>, filtros: () => FiltrosSugerencia = () => ({})): Sugeridor {
  const api = inject(Api);
  const consulta = signal<{ texto: string; filtros: FiltrosSugerencia } | null>(null);

  const opciones = toSignal(
    toObservable(consulta).pipe(
      debounceTime(ESPERA_MS),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      switchMap((c) =>
        c === null
          ? of<string[]>([])
          : from(api.invoke(sugerenciasActivo, { campo, ...(c.texto ? { texto: c.texto } : {}), ...c.filtros })).pipe(
              // Lo ya escrito igual no hace falta sugerirlo.
              map((lista) => lista.filter((v) => v !== c.texto)),
              catchError(() => of<string[]>([])),
            ),
      ),
    ),
    { initialValue: [] as string[] },
  );

  return {
    buscar: (texto) => consulta.set({ texto: (texto ?? '').trim(), filtros: filtrosConValor(filtros()) }),
    opciones,
  };
}
