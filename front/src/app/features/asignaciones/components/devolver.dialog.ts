import { ChangeDetectionStrategy, Component, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { devolverActivo } from '../../../api/fn/asignaciones/devolver-activo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { Conservacion } from '../../../api/models/conservacion';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { hoyIso } from '../../activos/data/formulario-activo';
import { TEXTO_CONSERVACION, formatoFecha } from '../../activos/data/textos-activo';
import { noPosteriorA } from '../../activos/data/validadores-activo';
import { estadoParaDevolucion } from '../data/asignacion';
import { noAnteriorAl } from './asignar.dialog';

export interface DatosDevolverDialog {
  readonly activoId: string;
  /** "PAT-0001 · Dell Latitude 5440". */
  readonly titulo: string;
  /** Quien lo devuelve. */
  readonly persona: string;
  /** Fecha de entrega ("aaaa-mm-dd"): la devolución no puede ser anterior. */
  readonly desde: string;
  /** Conservación que se propone (la de la entrega). */
  readonly conservacion: Conservacion;
}

/** Abre el diálogo; verdadero si se registró la devolución. */
export async function abrirDevolverDialog(dialogos: MatDialog, datos: DatosDevolverDialog): Promise<boolean> {
  const referencia = dialogos.open<DevolverDialog, DatosDevolverDialog, boolean>(DevolverDialog, { data: datos, width: '560px' });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

/**
 * Registra la devolución de un activo (D51): fecha, conservación, estado en que queda (se propone "En almacén") y
 * observaciones.
 */
@Component({
  selector: 'app-devolver-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>Devolución de {{ datos.persona }}</h2>
    <form [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
      <mat-dialog-content class="contenido">
        <span class="atenuado subtitulo">{{ datos.titulo }} · lo tenía desde el {{ formatoFecha(datos.desde) }}</span>
        @if (error(); as e) {
          <app-aviso>{{ e.mensaje }}</app-aviso>
        }
        <div class="rejilla">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Fecha de devolución</mat-label>
            <input matInput type="date" formControlName="fecha" [min]="datos.desde" [max]="hoy" required />
            @if (formulario.controls.fecha.hasError('required')) {
              <mat-error>Elija la fecha.</mat-error>
            } @else if (formulario.controls.fecha.hasError('futura')) {
              <mat-error>No puede ser posterior a hoy.</mat-error>
            } @else if (formulario.controls.fecha.hasError('anterior')) {
              <mat-error>No puede ser anterior a la entrega.</mat-error>
            }
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Conservación al devolver</mat-label>
            <mat-select formControlName="conservacion" required>
              @for (c of conservaciones; track c) {
                <mat-option [value]="c">{{ textoConservacion[c] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Estado del activo después de la devolución</mat-label>
          <mat-select formControlName="estadoActivoId" required>
            @for (e of estadosActivos(); track e.id) {
              <mat-option [value]="e.id">{{ e.nombre }}</mat-option>
            }
          </mat-select>
          <mat-hint>Se propone «En almacén». Cámbielo si el equipo va a mantenimiento o ya no funciona.</mat-hint>
          @if (formulario.controls.estadoActivoId.hasError('required')) {
            <mat-error>Elija el estado.</mat-error>
          }
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Observaciones (opcional)</mat-label>
          <textarea matInput formControlName="observaciones" rows="2" maxlength="500" placeholder="Por ejemplo: se devuelve sin cargador"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="guardando()">{{ guardando() ? 'Guardando…' : 'Registrar devolución' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; }
    .subtitulo { margin-top: -8px; }
    .rejilla { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    mat-form-field { width: 100%; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DevolverDialog {
  protected readonly datos = inject<DatosDevolverDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<DevolverDialog, boolean>>(MatDialogRef);

  protected readonly hoy = hoyIso();
  protected readonly conservaciones = Object.keys(TEXTO_CONSERVACION) as NonNullable<Conservacion>[];
  protected readonly textoConservacion = TEXTO_CONSERVACION;
  protected readonly formatoFecha = formatoFecha;

  protected readonly formulario = new FormGroup({
    fecha: new FormControl(this.hoy, {
      nonNullable: true,
      validators: [Validators.required, noPosteriorA(() => hoyIso()), noAnteriorAl(this.datos.desde)],
    }),
    conservacion: new FormControl<NonNullable<Conservacion>>(this.datos.conservacion ?? 'Bueno', { nonNullable: true }),
    estadoActivoId: new FormControl<string | null>(null, Validators.required),
    observaciones: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }),
  });

  private readonly estados = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'estados-activo' }),
  });

  protected readonly estadosActivos = computed(() => (this.estados.value() ?? []).filter((e) => e.activo));

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  constructor() {
    // Se propone "En almacén" al llegar el catálogo, si la persona todavía no eligió otro.
    effect(() => {
      const propuesto = estadoParaDevolucion(this.estadosActivos());
      untracked(() => {
        if (propuesto && !this.formulario.controls.estadoActivoId.value) {
          this.formulario.controls.estadoActivoId.setValue(propuesto);
        }
      });
    });
  }

  protected async guardar(): Promise<void> {
    if (this.formulario.invalid || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    const v = this.formulario.getRawValue();
    try {
      await this.api.invoke(devolverActivo, {
        id: this.datos.activoId,
        body: {
          fecha: v.fecha,
          conservacion: v.conservacion,
          estadoActivoId: v.estadoActivoId!,
          observaciones: v.observaciones.trim() === '' ? null : v.observaciones.trim(),
        },
      });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
