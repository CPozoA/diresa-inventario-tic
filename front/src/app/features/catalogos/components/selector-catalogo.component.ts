import {
  ChangeDetectionStrategy,
  Component,
  DoCheck,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NgControl } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInput, MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';

import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { buscarMismoNombre, coincidenciasAlEscribir } from '../data/busqueda-catalogo';
import { AgregarElementoDialog, DatosAgregarElemento } from './agregar-elemento.dialog';

/** Lo mínimo que necesita una opción: elementos de catálogo, establecimientos… */
export interface OpcionCatalogo {
  readonly id: string;
  readonly nombre: string;
  readonly activo: boolean;
}

/** Opción especial al final de la lista: agregar lo escrito al catálogo. */
interface OpcionAgregar {
  readonly agregar: string;
}

/**
 * Elige un elemento de catálogo escribiendo parte del nombre (D45): la lista se acorta mientras se escribe, pero el
 * valor siempre es uno del catálogo (su id). Quien puede modificar el catálogo ve, además, "Agregar «…» al catálogo".
 * Se usa como cualquier campo de formulario: <app-selector-catalogo formControlName="marcaId" …/>.
 */
@Component({
  selector: 'app-selector-catalogo',
  imports: [MatAutocompleteModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './selector-catalogo.component.html',
  styleUrl: './selector-catalogo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectorCatalogoComponent implements ControlValueAccessor, DoCheck {
  private readonly dialogos = inject(MatDialog);
  /** El control del formulario que usa este campo (para mostrar sus errores). */
  private readonly control = inject(NgControl, { self: true, optional: true });

  readonly etiqueta = input.required<string>();
  /** Elementos que se ofrecen (normalmente los activos y el ya elegido). */
  readonly elementos = input.required<readonly OpcionCatalogo[]>();
  readonly requerido = input(false);
  /** Texto de la opción "sin valor" (por ejemplo "Sin dato"); nulo si el campo no la ofrece. */
  readonly opcionVacia = input<string | null>(null);
  /** Mensaje de error cuando es obligatorio y está vacío. */
  readonly mensajeRequerido = input('Elija una opción de la lista.');
  readonly pista = input<string | null>(null);

  /** Para agregar al catálogo: su código y nombre en singular; sin código no se ofrece agregar. */
  readonly catalogo = input<string | null>(null);
  readonly nombreSingular = input('elemento');
  readonly puedeAgregar = input(false);
  /** Sede del catálogo (solo áreas). */
  readonly sedeId = input<string | null>(null);
  /** Todos los elementos del catálogo, también los desactivados (para avisar repetidos al agregar). */
  readonly existentes = input<readonly OpcionCatalogo[] | null>(null);
  /** Sin espacio reservado para mensajes debajo (en barras de filtros). */
  readonly compacto = input(false);

  /** Se agregó un elemento al catálogo: la pantalla lo suma a su lista. */
  readonly agregado = output<ElementoCatalogoDto>();

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');
  private readonly entrada = viewChild(MatInput);
  private readonly disparador = viewChild(MatAutocompleteTrigger);

  protected readonly valor = signal<string | null>(null);
  protected readonly texto = signal('');
  protected readonly desactivado = signal(false);
  /** "«Sony» no está en el catálogo", después de salir del campo con un texto que no corresponde. */
  protected readonly aviso = signal<string | null>(null);
  /** Nombre del elemento recién agregado, hasta que llegue en la lista. */
  private readonly agregadoReciente = signal<OpcionCatalogo | null>(null);

  private alCambiar: (valor: string | null) => void = () => undefined;
  private alTocar: () => void = () => undefined;

  protected readonly elegido = computed(() => {
    const id = this.valor();
    return this.elementos().find((e) => e.id === id) ?? (this.agregadoReciente()?.id === id ? this.agregadoReciente() : null);
  });

  /** Mientras el texto es el nombre del elegido, se ofrece la lista completa; al escribir, solo las coincidencias. */
  protected readonly opciones = computed(() => {
    const texto = this.texto();
    return texto === (this.elegido()?.nombre ?? '') ? [...this.elementos()] : coincidenciasAlEscribir(this.elementos(), texto);
  });

  /** Ofrece agregar lo escrito si no coincide exactamente con ninguno. */
  protected readonly textoParaAgregar = computed(() => {
    const texto = this.texto().replace(/\s+/g, ' ').trim();
    if (!this.catalogo() || !this.puedeAgregar() || texto === '' || texto === (this.elegido()?.nombre ?? '')) {
      return null;
    }
    return buscarMismoNombre(this.existentes() ?? this.elementos(), texto) ? null : texto;
  });

  /**
   * En error solo si el control del formulario es inválido (obligatorio y vacío). Lo escrito que no está en el catálogo
   * no deja el campo en error: se vuelve al valor anterior y se avisa debajo.
   */
  protected readonly enError = (): boolean => !!this.control?.invalid && !!(this.control.touched || this.control.dirty);

  protected readonly errorEstado: ErrorStateMatcher = { isErrorState: () => this.enError() };

  /** Mientras se abre el diálogo para agregar, cerrar la lista no debe tratarse como "escribió algo que no existe". */
  private agregando = false;

  constructor() {
    if (this.control) {
      this.control.valueAccessor = this;
    }

    // El campo muestra el nombre del elegido (también cuando la lista llega después del valor), salvo mientras se escribe.
    effect(() => {
      const nombre = this.elegido()?.nombre ?? '';
      untracked(() => {
        if (!this.campo() || document.activeElement !== this.campo()?.nativeElement) {
          this.texto.set(nombre);
        }
      });
    });
  }

  /**
   * El campo de texto interno no es el control del formulario: su estado de error (borde rojo, mensaje, aria-invalid)
   * se recalcula en cada revisión a partir del control externo y del aviso.
   */
  ngDoCheck(): void {
    this.entrada()?.updateErrorState();
  }

  // ---------- ControlValueAccessor ----------

  writeValue(valor: string | null): void {
    this.valor.set(valor ?? null);
    this.aviso.set(null);
  }

  registerOnChange(fn: (valor: string | null) => void): void {
    this.alCambiar = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.alTocar = fn;
  }

  setDisabledState(desactivado: boolean): void {
    this.desactivado.set(desactivado);
  }

  // ---------- Interacción ----------

  protected mostrar = (opcion: OpcionCatalogo | OpcionAgregar | string | null): string => {
    if (opcion === null || typeof opcion === 'string') {
      return opcion ?? '';
    }
    return 'agregar' in opcion ? opcion.agregar : opcion.nombre;
  };

  protected alEscribir(evento: Event): void {
    this.texto.set((evento.target as HTMLInputElement).value);
    this.aviso.set(null);
  }

  /** Tocar el campo ya enfocado (por ejemplo en el celular) vuelve a mostrar la lista. */
  protected alTocarCampo(): void {
    const disparador = this.disparador();
    if (disparador && !disparador.panelOpen && !this.desactivado()) {
      disparador.openPanel();
    }
  }

  protected alEntrar(): void {
    // Seleccionar todo: escribir reemplaza el valor anterior.
    this.campo()?.nativeElement.select();
  }

  protected async alElegir(evento: MatAutocompleteSelectedEvent): Promise<void> {
    const opcion = evento.option.value as OpcionCatalogo | OpcionAgregar | null;
    if (opcion !== null && 'agregar' in opcion) {
      this.agregando = true;
      try {
        await this.agregar(opcion.agregar);
      } finally {
        this.agregando = false;
      }
      return;
    }
    this.fijar(opcion);
  }

  /**
   * Al cerrar la lista (por elegir, salir del campo o Esc): si lo escrito no es una opción, se busca por nombre exacto;
   * si no existe, se vuelve al valor anterior y se avisa.
   */
  protected alCerrar(): void {
    // Espera a que se procese una posible elección.
    setTimeout(() => {
      if (this.agregando) {
        return;
      }
      this.alTocar();
      const texto = this.texto().replace(/\s+/g, ' ').trim();
      const elegido = this.elegido();
      if (texto === (elegido?.nombre ?? '')) {
        return;
      }
      if (texto === '') {
        this.fijar(null);
        return;
      }
      const exacto = buscarMismoNombre(this.elementos(), texto);
      if (exacto) {
        this.fijar(exacto);
        return;
      }
      this.aviso.set(
        this.puedeAgregar() && this.catalogo()
          ? `«${texto}» no está en el catálogo: elija uno de la lista o agréguelo.`
          : `«${texto}» no está en el catálogo. Si falta, pídale al administrador que lo agregue.`,
      );
      this.texto.set(elegido?.nombre ?? '');
      this.ponerTexto(elegido?.nombre ?? '');
    });
  }

  private fijar(elemento: OpcionCatalogo | null): void {
    this.aviso.set(null);
    this.texto.set(elemento?.nombre ?? '');
    this.ponerTexto(elemento?.nombre ?? '');
    if (this.valor() !== (elemento?.id ?? null)) {
      this.valor.set(elemento?.id ?? null);
      this.alCambiar(elemento?.id ?? null);
    }
  }

  /** El autocompletado escribe en el campo por su cuenta; se deja el texto que corresponde. */
  private ponerTexto(texto: string): void {
    const campo = this.campo()?.nativeElement;
    if (campo) {
      campo.value = texto;
    }
  }

  private async agregar(nombre: string): Promise<void> {
    const datos: DatosAgregarElemento = {
      tipo: this.catalogo()!,
      nombreSingular: this.nombreSingular(),
      nombre,
      sedeId: this.sedeId(),
      existentes: this.existentes() ?? this.elementos(),
    };
    const nuevo = await firstValueFrom(
      this.dialogos
        .open<AgregarElementoDialog, DatosAgregarElemento, ElementoCatalogoDto>(AgregarElementoDialog, {
          data: datos,
          width: '480px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );

    if (nuevo) {
      this.agregadoReciente.set(nuevo);
      this.agregado.emit(nuevo);
      this.fijar(nuevo);
    } else {
      // Canceló: el campo vuelve al valor que tenía.
      this.fijar(this.elegido());
    }
  }
}
