import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, model, signal, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, combineLatest, debounceTime, distinctUntilChanged, from, map, of, startWith, switchMap, tap } from 'rxjs';

import { Api } from '../../../api/api';
import { listarPersonal } from '../../../api/fn/personal/listar-personal';
import { obtenerPersona } from '../../../api/fn/personal/obtener-persona';
import { PersonaResumenDto } from '../../../api/models/persona-resumen-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { PersonaElegida, detallePersona, personaElegida } from '../data/asignacion';
import { iniciales, limpiarDni } from '../data/personal';
import { abrirPersonaDialog } from './persona.dialog';

const MAXIMO_RESULTADOS = 8;
const ESPERA_MS = 300;

/** Valor especial de la lista: registrar a alguien que todavía no está en el personal. */
const NUEVA = 'nueva';

/**
 * Elige a quién asignar un equipo escribiendo su DNI o parte de su nombre (D51). Solo ofrece al personal activo de la
 * red. Si la persona no está, "Registrar a una persona nueva" abre el mismo diálogo de Asignaciones y la deja elegida.
 */
@Component({
  selector: 'app-buscador-persona',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './buscador-persona.component.html',
  styleUrl: './buscador-persona.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuscadorPersonaComponent {
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  /** Red del equipo (nula para un usuario de sede: la API usa la suya). */
  readonly sedeId = input<string | null>(null);
  /** Quien ya tiene el equipo (al reasignar no se ofrece). */
  readonly excluirId = input<string | null>(null);
  readonly etiqueta = input('Persona');
  readonly marcarError = input(false);
  readonly elegido = model<PersonaElegida | null>(null);

  protected readonly nueva = NUEVA;
  protected readonly iniciales = iniciales;
  protected readonly detallePersona = detallePersona;
  protected readonly busqueda = new FormControl<string | PersonaResumenDto>('', { nonNullable: true });
  protected readonly buscando = signal(false);

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');

  protected readonly texto = toSignal(this.busqueda.valueChanges.pipe(map((v) => (typeof v === 'string' ? v.trim() : ''))), {
    initialValue: '',
  });

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
          ? of<PersonaResumenDto[]>([])
          : from(this.api.invoke(listarPersonal, { buscar: texto, maximo: MAXIMO_RESULTADOS, ...(sedeId ? { sedeId } : {}) })).pipe(
              catchError(() => of<PersonaResumenDto[]>([])),
            ),
      ),
      tap(() => this.buscando.set(false)),
    ),
    { initialValue: [] as PersonaResumenDto[] },
  );

  protected readonly opciones = computed(() => this.resultados().filter((p) => p.id !== this.excluirId()));

  protected readonly sinCoincidencias = computed(() => this.texto() !== '' && !this.buscando() && this.opciones().length === 0);

  /** El campo muestra lo escrito, no el objeto elegido (al elegir, el campo desaparece). */
  protected readonly mostrar = (valor: string | PersonaResumenDto | null): string =>
    typeof valor === 'string' ? valor : (valor?.nombreCompleto ?? '');

  protected elegir(evento: MatAutocompleteSelectedEvent): void {
    const valor = evento.option.value as PersonaResumenDto | typeof NUEVA;
    if (valor === NUEVA) {
      this.busqueda.setValue(this.texto(), { emitEvent: false });
      void this.registrarNueva();
      return;
    }
    this.elegido.set(personaElegida(valor));
    this.busqueda.setValue('');
  }

  protected quitar(): void {
    this.elegido.set(null);
    // El campo aparece en el siguiente ciclo de detección.
    setTimeout(() => this.campo()?.nativeElement.focus());
  }

  /** Abre "Nueva persona" (con el DNI si ya se escribió) y deja elegida a la persona registrada. */
  protected async registrarNueva(): Promise<void> {
    const dni = limpiarDni(this.texto());
    const resultado = await abrirPersonaDialog(this.dialogos, {
      persona: null,
      sedeId: this.sedeId(),
      dni: dni.length === 8 ? dni : undefined,
    });
    if (!resultado) {
      return;
    }
    try {
      const p = await this.api.invoke(obtenerPersona, { id: resultado.id });
      this.elegido.set({ id: p.id, nombreCompleto: p.nombreCompleto, dni: p.dni, detalle: detallePersona(p) });
      this.busqueda.setValue('');
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }
}
