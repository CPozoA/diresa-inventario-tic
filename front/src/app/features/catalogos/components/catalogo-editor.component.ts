import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
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
import { DatosRenombrar, RenombrarDialog } from './renombrar.dialog';

/**
 * Lista de un catálogo con alta rápida, renombrar y activar o desactivar.
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
  protected readonly nuevo = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] });
    /** Agrupa el alta para que (ngSubmit) funcione y el navegador no envíe el formulario recargando la página. */
  protected readonly alta = new FormGroup({ nombre: this.nuevo });
  protected readonly agregando = signal(false);
  protected readonly errorAlta = signal<ErrorApi | null>(null);

  protected readonly elementos = resource({
    params: () => ({ tipo: this.tipo().codigo, sedeId: this.sedeId() ?? undefined }),
    loader: ({ params }) => this.api.invoke(listarElementosCatalogo, { ...params, incluirInactivos: true }),
  });

  protected readonly visibles = computed(() => {
    const lista = this.elementos.hasValue() ? this.elementos.value() : [];
    return this.mostrarInactivos() ? lista : lista.filter((e) => e.activo);
  });

  protected readonly cantidadInactivos = computed(() =>
    this.elementos.hasValue() ? this.elementos.value().filter((e) => !e.activo).length : 0,
  );

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }

  protected async agregar(): Promise<void> {
    if (this.nuevo.invalid || this.agregando()) {
      this.nuevo.markAsTouched();
      return;
    }

    this.agregando.set(true);
    this.errorAlta.set(null);

    try {
      await this.api.invoke(crearElementoCatalogo, {
        tipo: this.tipo().codigo,
        body: { sedeId: this.sedeId(), nombre: this.nuevo.value },
      });
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
    try {
      const params = { tipo: this.tipo().codigo, id: elemento.id };
      if (elemento.activo) {
        await this.api.invoke(desactivarElementoCatalogo, params);
      } else {
        await this.api.invoke(activarElementoCatalogo, params);
      }
      this.notificaciones.exito(
        elemento.activo ? `«${elemento.nombre}» ya no se ofrecerá al registrar.` : `«${elemento.nombre}» está activo otra vez.`,
      );
      this.elementos.reload();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }
}
