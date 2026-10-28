import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { asignarActivo } from '../../../api/fn/asignaciones/asignar-activo';
import { AsignacionActivoDto } from '../../../api/models/asignacion-activo-dto';
import { Conservacion } from '../../../api/models/conservacion';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { hoyIso } from '../../activos/data/formulario-activo';
import { TEXTO_CONSERVACION, formatoFecha } from '../../activos/data/textos-activo';
import { noPosteriorA } from '../../activos/data/validadores-activo';
import { PersonaElegida } from '../data/asignacion';
import { BuscadorPersonaComponent } from './buscador-persona.component';

/** La fecha ("aaaa-mm-dd") no puede ser anterior a otra fija (el último movimiento del equipo). */
export function noAnteriorAl(desde: string | null): ValidatorFn {
  return (c) => (desde && c.value && String(c.value) < desde ? { anterior: true } : null);
}

export interface DatosAsignarDialog {
  readonly activoId: string;
  /** "PAT-0001 · Dell Latitude 5440". */
  readonly titulo: string;
  readonly sedeId: string;
  /** Conservación actual del activo: se propone al entregarlo. */
  readonly conservacion: Conservacion;
  /** Quien lo tiene ahora (reasignar), o nula. */
  readonly actual: AsignacionActivoDto | null;
  /** Último movimiento del equipo ("aaaa-mm-dd"): la entrega no puede ser anterior. */
  readonly desde: string | null;
}

/** Abre el diálogo; verdadero si se asignó. */
export async function abrirAsignarDialog(dialogos: MatDialog, datos: DatosAsignarDialog): Promise<boolean> {
  const referencia = dialogos.open<AsignarDialog, DatosAsignarDialog, boolean>(AsignarDialog, { data: datos, width: '560px' });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

/**
 * Asignar un activo a una persona, o reasignarlo a otra (D51). Al reasignar, quien lo tenía lo devuelve con la misma
 * fecha y conservación.
 */
@Component({
  selector: 'app-asignar-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, AvisoComponent, BuscadorPersonaComponent],
  template: `
    <h2 mat-dialog-title>{{ datos.actual ? 'Reasignar a otra persona' : 'Asignar a una persona' }}</h2>
    <form [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
      <mat-dialog-content class="contenido">
        <span class="atenuado subtitulo">{{ datos.titulo }}</span>
        @if (error(); as e) {
          <app-aviso>{{ e.mensaje }}</app-aviso>
        }
        <app-buscador-persona
          [sedeId]="datos.sedeId"
          [excluirId]="datos.actual?.personaId ?? null"
          [(elegido)]="persona"
          [marcarError]="intento() && !persona()"
        />
        <div class="rejilla">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Fecha de entrega</mat-label>
            <input matInput type="date" formControlName="fecha" [max]="hoy" [min]="datos.desde ?? ''" required />
            @if (formulario.controls.fecha.hasError('required')) {
              <mat-error>Elija la fecha.</mat-error>
            } @else if (formulario.controls.fecha.hasError('futura')) {
              <mat-error>No puede ser posterior a hoy.</mat-error>
            } @else if (formulario.controls.fecha.hasError('anterior')) {
              <mat-error>No puede ser anterior al {{ formatoFecha(datos.desde) }}.</mat-error>
            }
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Conservación al entregar</mat-label>
            <mat-select formControlName="conservacion" required>
              @for (c of conservaciones; track c) {
                <mat-option [value]="c">{{ textoConservacion[c] }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
        @if (datos.desde) {
          <span class="atenuado nota">El último movimiento del equipo fue el {{ formatoFecha(datos.desde) }}: la entrega no puede ser anterior.</span>
        }
        @if (datos.actual; as a) {
          <app-aviso tono="info">Se cerrará la asignación de {{ a.nombreCompleto }} con esta misma fecha y conservación.</app-aviso>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="guardando()">
          {{ guardando() ? 'Guardando…' : datos.actual ? 'Reasignar' : 'Asignar' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; }
    .subtitulo { margin-top: -8px; }
    .rejilla { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    mat-form-field { width: 100%; }
    .nota { font-size: 13px; margin-top: -6px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AsignarDialog {
  protected readonly datos = inject<DatosAsignarDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<AsignarDialog, boolean>>(MatDialogRef);

  protected readonly hoy = hoyIso();
  protected readonly conservaciones = Object.keys(TEXTO_CONSERVACION) as NonNullable<Conservacion>[];
  protected readonly textoConservacion = TEXTO_CONSERVACION;
  protected readonly formatoFecha = formatoFecha;

  protected readonly formulario = new FormGroup({
    fecha: new FormControl(this.hoy, { nonNullable: true, validators: [Validators.required, noPosteriorA(() => hoyIso()), noAnteriorAl(this.datos.desde)] }),
    conservacion: new FormControl<NonNullable<Conservacion>>(this.datos.conservacion ?? 'Bueno', { nonNullable: true }),
  });

  protected readonly persona = signal<PersonaElegida | null>(null);
  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected async guardar(): Promise<void> {
    this.intento.set(true);
    const persona = this.persona();
    if (this.formulario.invalid || !persona || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);
    const v = this.formulario.getRawValue();
    try {
      await this.api.invoke(asignarActivo, {
        id: this.datos.activoId,
        body: { personaId: persona.id, fechaEntrega: v.fecha, conservacion: v.conservacion },
      });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
