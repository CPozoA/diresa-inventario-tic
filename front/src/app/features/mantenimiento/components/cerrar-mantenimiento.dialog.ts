import { ChangeDetectionStrategy, Component, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { obtenerFichaActivo } from '../../../api/fn/activos/obtener-ficha-activo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { cerrarMantenimiento } from '../../../api/fn/mantenimiento/cerrar-mantenimiento';
import { obtenerMantenimientosActivo } from '../../../api/fn/mantenimiento/obtener-mantenimientos-activo';
import { MantenimientoDto } from '../../../api/models/mantenimiento-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { hoyIso } from '../../activos/data/formulario-activo';
import { aNumero, formatoFecha } from '../../activos/data/textos-activo';
import { proximoPropuesto, quien, textoTipo } from '../data/mantenimiento';
import { CamposCierreComponent, cierreDesdeFormulario, crearFormularioCierre } from './campos-cierre.component';
import { valorDe } from '../../../shared/util/recurso';

export interface DatosCerrarMantenimiento {
  readonly mantenimiento: MantenimientoDto;
}

/** Abre el diálogo; verdadero si se cerró. */
export async function abrirCerrarMantenimiento(dialogos: MatDialog, mantenimiento: MantenimientoDto): Promise<boolean> {
  const referencia = dialogos.open<CerrarMantenimientoDialog, DatosCerrarMantenimiento, boolean>(CerrarMantenimientoDialog, {
    data: { mantenimiento },
    width: '640px',
  });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

/**
 * Cerrar un mantenimiento (D53): término, trabajo realizado, resultado, estado y conservación en que queda el activo y
 * próximo preventivo (en un preventivo se propone a 6 meses; en un correctivo, el que ya tenía).
 */
@Component({
  selector: 'app-cerrar-mantenimiento-dialog',
  imports: [MatButtonModule, MatDialogModule, MatProgressBarModule, AvisoComponent, CamposCierreComponent],
  template: `
    <h2 mat-dialog-title>Cerrar mantenimiento</h2>
    <mat-dialog-content class="contenido">
      <span class="atenuado subtitulo">{{ subtitulo }}</span>
      @if (error(); as e) {
        <app-aviso>{{ e.mensaje }}</app-aviso>
      }
      @if (ficha.isLoading() || delActivo.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <form id="formulario-cierre" novalidate (submit)="$event.preventDefault(); guardar()">
        <app-campos-cierre
          [grupo]="cierre"
          [estados]="valorDe(estados) ?? []"
          [conProveedor]="m.ejecutor === 'Proveedor'"
          [tipo]="m.tipo"
          [minima]="m.fechaInicio"
        />
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="submit" form="formulario-cierre" [disabled]="guardando()">
        {{ guardando() ? 'Guardando…' : 'Cerrar mantenimiento' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; }
    /* Espacio para que no se corten el subtítulo ni la etiqueta del primer campo. */
    .contenido.mat-mdc-dialog-content { padding-top: 8px; }
    .subtitulo { margin-top: -8px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CerrarMantenimientoDialog {
  protected readonly valorDe = valorDe;
  protected readonly m = inject<DatosCerrarMantenimiento>(MAT_DIALOG_DATA).mantenimiento;
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<CerrarMantenimientoDialog, boolean>>(MatDialogRef);

  protected readonly subtitulo = `${this.m.codigoPatrimonial} · ${this.m.marca} ${this.m.modelo} · ${textoTipo(this.m.tipo)} desde el ${formatoFecha(this.m.fechaInicio)} · ${quien(this.m).principal}`;

  protected readonly cierre = crearFormularioCierre(() => this.m.fechaInicio, {
    conservacion: 'Bueno',
    proximo: this.m.tipo === 'Preventivo' ? proximoPropuesto('Preventivo', hoyIso(), null) : null,
    costo: aNumero(this.m.costo),
  }, () => this.proximoActual());

  protected readonly ficha = resource({
    loader: () => this.api.invoke(obtenerFichaActivo, { id: this.m.activoId }),
  });

  protected readonly delActivo = resource({
    loader: () => this.api.invoke(obtenerMantenimientosActivo, { id: this.m.activoId }),
  });

  protected readonly estados = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'estados-activo', incluirInactivos: true }),
  });

  private readonly proximoActual = computed(() => valorDe(this.delActivo)?.proximoMantenimiento ?? null);

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  constructor() {
    // La conservación actual del activo y, en un correctivo, el próximo preventivo que ya tenía.
    effect(() => {
      const f = valorDe(this.ficha);
      const actual = this.proximoActual();
      const cargado = this.delActivo.hasValue();
      untracked(() => {
        if (f && !this.cierre.controls.conservacion.dirty) {
          this.cierre.controls.conservacion.setValue(f.generales.conservacion ?? 'Bueno');
        }
        if (cargado && this.m.tipo !== 'Preventivo' && !this.cierre.controls.proximo.dirty) {
          this.cierre.controls.proximo.setValue(actual ?? '');
        }
      });
    });
  }

  protected async guardar(): Promise<void> {
    if (this.cierre.invalid || this.guardando()) {
      this.cierre.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(cerrarMantenimiento, {
        id: this.m.id,
        body: cierreDesdeFormulario(this.cierre, this.m.ejecutor === 'Proveedor', this.proximoActual()),
      });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
