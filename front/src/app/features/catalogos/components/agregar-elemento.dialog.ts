import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Api } from '../../../api/api';
import { cambiarFichaTipoActivo } from '../../../api/fn/catalogos/cambiar-ficha-tipo-activo';
import { cambiarGeneracionesProcesador } from '../../../api/fn/catalogos/cambiar-generaciones-procesador';
import { crearElementoCatalogo } from '../../../api/fn/catalogos/crear-elemento-catalogo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { FichaTecnica } from '../../../api/models/ficha-tecnica';
import { GeneracionesProcesador } from '../../../api/models/generaciones-procesador';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import {
  FAMILIAS_GENERACIONES,
  FICHAS,
  descripcionFicha,
  descripcionGeneraciones,
  textoFicha,
  textoGeneraciones,
} from '../../activos/data/textos-activo';
import { buscarMismoNombre, nombresParecidos } from '../data/busqueda-catalogo';

export interface DatosAgregarElemento {
  /** Código del catálogo ("marcas", "areas"…). */
  readonly tipo: string;
  /** "Marca", "Área"… */
  readonly nombreSingular: string;
  /** Lo que el usuario escribió en el campo. */
  readonly nombre: string;
  /** Solo en catálogos de sede (áreas). */
  readonly sedeId: string | null;
  /** Elementos que ya tiene el catálogo (activos y desactivados), para avisar repetidos y parecidos. */
  readonly existentes: readonly { readonly id: string; readonly nombre: string; readonly activo: boolean }[];
}

/** "Automática": la toma el sistema por el nombre (D43, D44). */
const AUTOMATICA = 'auto';

/**
 * Agrega un elemento a un catálogo sin salir del formulario del activo (D45). Usa la misma API y los mismos permisos
 * que Catálogos generales; avisa si ya existe uno igual o parecido. Devuelve el elemento creado.
 */
@Component({
  selector: 'app-agregar-elemento-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, AvisoComponent],
  templateUrl: './agregar-elemento.dialog.html',
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; min-width: min(420px, 80vw); }
    mat-form-field { width: 100%; }
    .opcion { display: flex; flex-direction: column; line-height: 1.3; padding: 4px 0; }
    .opcion .atenuado { font-size: 12px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgregarElementoDialog {
  protected readonly datos = inject<DatosAgregarElemento>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<AgregarElementoDialog, ElementoCatalogoDto>>(MatDialogRef);

  protected readonly automatica = AUTOMATICA;
  protected readonly fichas = FICHAS;
  protected readonly familias = FAMILIAS_GENERACIONES;
  protected readonly textoFicha = textoFicha;
  protected readonly descripcionFicha = descripcionFicha;
  protected readonly textoGeneraciones = textoGeneraciones;
  protected readonly descripcionGeneraciones = descripcionGeneraciones;

  protected readonly esTipoActivo = this.datos.tipo === 'tipos-activo';
  protected readonly esProcesador = this.datos.tipo === 'procesadores';

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    nombre: [this.datos.nombre.replace(/\s+/g, ' ').trim(), [Validators.required, Validators.maxLength(100)]],
    ficha: [AUTOMATICA as string],
    generaciones: [AUTOMATICA as string],
  });

  private readonly nombre = toSignal(this.formulario.controls.nombre.valueChanges, {
    initialValue: this.formulario.controls.nombre.value,
  });

  /** Ya existe con ese nombre (sin distinguir mayúsculas ni tildes). */
  protected readonly repetido = computed(() => buscarMismoNombre(this.datos.existentes, this.nombre()));

  protected readonly parecidos = computed(() => nombresParecidos(this.datos.existentes, this.nombre()));

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected async agregar(): Promise<void> {
    if (this.formulario.invalid || this.repetido() || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    const { nombre, ficha, generaciones } = this.formulario.getRawValue();
    const limpio = nombre.replace(/\s+/g, ' ').trim();

    try {
      const { id } = await this.api.invoke(crearElementoCatalogo, {
        tipo: this.datos.tipo,
        body: { sedeId: this.datos.sedeId, nombre: limpio },
      });

      if (this.esTipoActivo && ficha !== AUTOMATICA) {
        await this.api.invoke(cambiarFichaTipoActivo, { id, body: { ficha: ficha as NonNullable<FichaTecnica> } });
      }
      if (this.esProcesador && generaciones !== AUTOMATICA) {
        await this.api.invoke(cambiarGeneracionesProcesador, {
          id,
          body: { generaciones: generaciones as NonNullable<GeneracionesProcesador> },
        });
      }

      // Se vuelve a leer para tener la ficha o las generaciones que asignó el sistema.
      const lista = await this.api.invoke(listarElementosCatalogo, {
        tipo: this.datos.tipo,
        incluirInactivos: true,
        ...(this.datos.sedeId ? { sedeId: this.datos.sedeId } : {}),
      });
      this.referencia.close(
        lista.find((e) => e.id === id) ?? { id, nombre: limpio, activo: true, ficha: null, generaciones: null, grupo: null },
      );
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
