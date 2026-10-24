import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { activarElementoCatalogo } from '../../../api/fn/catalogos/activar-elemento-catalogo';
import { crearElementoCatalogo } from '../../../api/fn/catalogos/crear-elemento-catalogo';
import { desactivarElementoCatalogo } from '../../../api/fn/catalogos/desactivar-elemento-catalogo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { TipoCatalogoDto } from '../../../api/models/tipo-catalogo-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { buscarMismoNombre, filtrarPorNombre, normalizarNombre } from '../data/busqueda-catalogo';
import { DatosRenombrar, RenombrarDialog } from './renombrar.dialog';

/**
 * Lista de un catálogo con un solo campo para buscar y agregar, renombrar y activar o desactivar.
 * Al escribir, la tabla se filtra y avisa si el nombre ya existe (sin distinguir mayúsculas ni tildes).
 * Sirve para todos los catálogos: cambia solo el tipo (y la sede, en los de sede).
 */
@Component({
  selector: 'app-catalogo-editor',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    AvisoComponent,
  ],
  templateUrl: './catalogo-editor.component.html',
  styleUrl: './catalogo-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogoEditorComponent {
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  readonly tipo = input.required<TipoCatalogoDto>();

  /** Sede del catálogo (catálogos de sede consultados por el superadministrador). */
  readonly sedeId = input<string | null>(null);

  protected readonly mostrarInactivos = signal(false);
  // Sin "required": el campo también sirve para buscar, así que vacío es un estado normal (puedeAgregar lo controla).
  protected readonly nuevo = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(100)] });
  /** Agrupa el alta para que (ngSubmit) funcione y el navegador no envíe el formulario recargando la página. */
  protected readonly alta = new FormGroup({ nombre: this.nuevo });
  protected readonly agregando = signal(false);
  protected readonly errorAlta = signal<ErrorApi | null>(null);

  /** Lo escrito en el campo: filtra la tabla y es el nombre que se agrega. */
  protected readonly texto = toSignal(this.nuevo.valueChanges, { initialValue: this.nuevo.value });

  protected readonly elementos = resource({
    params: () => ({ tipo: this.tipo().codigo, sedeId: this.sedeId() ?? undefined }),
    loader: ({ params }) => this.api.invoke(listarElementosCatalogo, { ...params, incluirInactivos: true }),
  });

  private readonly todos = computed(() => (this.elementos.hasValue() ? this.elementos.value() : []));

  protected readonly buscando = computed(() => normalizarNombre(this.texto()) !== '');

  /** Al buscar se muestran también los desactivados, para que se vea si el nombre ya existe. */
  protected readonly visibles = computed(() => {
    if (this.buscando()) {
      return filtrarPorNombre(this.todos(), this.texto());
    }
    return this.mostrarInactivos() ? this.todos() : this.todos().filter((e) => e.activo);
  });

  protected readonly cantidadTotal = computed(() => this.todos().length);

  protected readonly cantidadInactivos = computed(() => this.todos().filter((e) => !e.activo).length);

  /** Elemento que ya tiene exactamente el nombre escrito (activo o desactivado). */
  protected readonly repetido = computed(() => buscarMismoNombre(this.todos(), this.texto()));

  protected readonly puedeAgregar = computed(
    () => this.tipo().puedeGestionar && this.buscando() && this.repetido() === undefined && !this.agregando(),
  );

  protected readonly etiqueta = computed(() => {
    const singular = this.tipo().nombreSingular.toLowerCase();
    return this.tipo().puedeGestionar ? `Buscar o agregar ${singular}` : `Buscar ${singular}`;
  });

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }

  protected limpiar(): void {
    this.nuevo.reset();
    this.errorAlta.set(null);
  }

  protected async agregar(): Promise<void> {
    if (!this.puedeAgregar() || this.nuevo.invalid) {
      this.nuevo.markAsTouched();
      return;
    }

    const nombre = this.nuevo.value.replace(/\s+/g, ' ').trim();
    this.agregando.set(true);
    this.errorAlta.set(null);

    try {
      await this.api.invoke(crearElementoCatalogo, {
        tipo: this.tipo().codigo,
        body: { sedeId: this.sedeId(), nombre },
      });
      this.notificaciones.exito(`«${nombre}» se agregó al catálogo.`);
      this.nuevo.reset();
      this.elementos.reload();
    } catch (error: unknown) {
      this.errorAlta.set(aErrorApi(error));
    } finally {
      this.agregando.set(false);
    }
  }

  protected async renombrar(elemento: ElementoCatalogoDto): Promise<void> {
    const datos: DatosRenombrar = { tipo: this.tipo().codigo, nombreSingular: this.tipo().nombreSingular, elemento };
    const guardado = await firstValueFrom(
      this.dialogos.open<RenombrarDialog, DatosRenombrar, boolean>(RenombrarDialog, { data: datos }).afterClosed(),
    );

    if (guardado) {
      this.notificaciones.exito('Nombre actualizado.');
      this.elementos.reload();
    }
  }

  protected async cambiarEstado(elemento: ElementoCatalogoDto): Promise<void> {
    const activar = !elemento.activo;

    if (!activar) {
      const confirmado = await confirmar(this.dialogos, {
        titulo: `Desactivar «${elemento.nombre}»`,
        mensaje:
          'Dejará de ofrecerse al registrar, pero se conserva en los registros que ya lo usan. Puede activarlo de nuevo cuando lo necesite.',
        confirmar: 'Desactivar',
      });
      if (!confirmado) {
        return;
      }
    }

    try {
      const params = { tipo: this.tipo().codigo, id: elemento.id };
      if (activar) {
        await this.api.invoke(activarElementoCatalogo, params);
      } else {
        await this.api.invoke(desactivarElementoCatalogo, params);
      }
      this.notificaciones.exito(
        activar ? `«${elemento.nombre}» está activo otra vez.` : `«${elemento.nombre}» ya no se ofrecerá al registrar.`,
      );
      this.elementos.reload();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }
}
