import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, model, signal, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, combineLatest, debounceTime, distinctUntilChanged, from, map, of, startWith, switchMap, tap } from 'rxjs';

import { Api } from '../../../api/api';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';

/** Establecimiento elegido para el activo: lo que el formulario necesita mostrar y enviar. */
export interface EstablecimientoElegido {
  readonly id: string;
  readonly sedeId: string;
  readonly codigoUnico: number | string;
  readonly nombre: string;
  /** "Categoría I-2 · Microred Luricocha · Luricocha, Huanta". */
  readonly detalle: string;
}

const MAXIMO_RESULTADOS = 8;
const ESPERA_MS = 300;

/** Detalle de un establecimiento en una línea; con varias redes a la vista, agrega la red. */
export function detalleDeEstablecimiento(e: EstablecimientoDto, conRed: boolean): string {
  const lugar = [e.distrito, e.provincia].filter((x) => !!x).join(', ');
  return [e.categoria ? `Categoría ${e.categoria}` : null, e.microred, lugar, conRed ? e.sedeNombre : null]
    .filter((x) => !!x)
    .join(' · ');
}

/**
 * Elige el establecimiento del activo escribiendo su código único o parte del nombre (D39). Solo ofrece los activos
 * de la red indicada; sin red (superadministrador con "Todas las sedes"), los de todas.
 */
@Component({
  selector: 'app-buscador-establecimiento',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './buscador-establecimiento.component.html',
  styleUrl: './buscador-establecimiento.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuscadorEstablecimientoComponent {
  private readonly api = inject(Api);

  /** Red donde buscar (nula = todas, solo para el superadministrador). */
  readonly sedeId = input<string | null>(null);

  /** Muestra error si se intentó guardar sin elegir. */
  readonly marcarError = input(false);

  readonly elegido = model<EstablecimientoElegido | null>(null);

  protected readonly busqueda = new FormControl<string | EstablecimientoDto>('', { nonNullable: true });
  protected readonly buscando = signal(false);

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');

  private readonly texto = toSignal(
    this.busqueda.valueChanges.pipe(map((v) => (typeof v === 'string' ? v.trim() : ''))),
    { initialValue: '' },
  );

  protected readonly resultados = toSignal(
    combineLatest([
      this.busqueda.valueChanges.pipe(
        map((v) => (typeof v === 'string' ? v.trim() : '')),
        debounceTime(ESPERA_MS),
        distinctUntilChanged(),
        startWith(''),
      ),
      toObservable(this.sedeId),
    ]).pipe(
      tap(([texto]) => this.buscando.set(texto !== '')),
      switchMap(([texto, sedeId]) =>
        texto === ''
          ? of<EstablecimientoDto[]>([])
          : from(
              this.api.invoke(listarEstablecimientos, {
                buscar: texto,
                maximo: MAXIMO_RESULTADOS,
                ...(sedeId ? { sedeId } : {}),
              }),
            ).pipe(catchError(() => of<EstablecimientoDto[]>([]))),
      ),
      tap(() => this.buscando.set(false)),
    ),
    { initialValue: [] as EstablecimientoDto[] },
  );

  protected readonly sinCoincidencias = computed(
    () => this.texto() !== '' && !this.buscando() && this.resultados().length === 0,
  );

  protected readonly conRed = computed(() => this.sedeId() === null);

  protected detalle(e: EstablecimientoDto): string {
    return detalleDeEstablecimiento(e, this.conRed());
  }

  /** El campo muestra el texto escrito, no el objeto elegido (al elegir, el campo desaparece). */
  protected readonly mostrar = (valor: string | EstablecimientoDto | null): string =>
    typeof valor === 'string' ? valor : (valor?.nombre ?? '');

  protected elegir(evento: MatAutocompleteSelectedEvent): void {
    const e = evento.option.value as EstablecimientoDto;
    this.elegido.set({
      id: e.id,
      sedeId: e.sedeId,
      codigoUnico: e.codigoUnico,
      nombre: e.nombre,
      detalle: detalleDeEstablecimiento(e, this.conRed()),
    });
    this.busqueda.setValue('');
  }

  protected cambiar(): void {
    this.elegido.set(null);
    // El campo aparece en el siguiente ciclo de detección.
    setTimeout(() => this.campo()?.nativeElement.focus());
  }
}
