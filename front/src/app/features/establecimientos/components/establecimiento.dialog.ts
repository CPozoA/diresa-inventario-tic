import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Api } from '../../../api/api';
import { crearEstablecimiento } from '../../../api/fn/establecimientos/crear-establecimiento';
import { editarEstablecimiento } from '../../../api/fn/establecimientos/editar-establecimiento';
import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';
import { SedeDto } from '../../../api/models/sede-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';

export interface DatosEstablecimientoDialog {
  /** Nulo para registrar uno nuevo. */
  readonly establecimiento: EstablecimientoDto | null;
  /** Redes que se pueden elegir al registrar. */
  readonly sedes: readonly SedeDto[];
  /** Red elegida de antemano (la del selector del encabezado). */
  readonly sedeId: string | null;
}

const NUMERO = /^[0-9]{1,9}$/;
const UBIGEO = /^[0-9]{6}$/;

/** Registrar o corregir un establecimiento. La red solo se elige al registrarlo: después no cambia. */
@Component({
  selector: 'app-establecimiento-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>{{ datos.establecimiento ? 'Editar establecimiento' : 'Nuevo establecimiento' }}</h2>
    <form [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
      <mat-dialog-content>
        @if (error(); as e) {
          <app-aviso>{{ e.mensaje }}</app-aviso>
        }
        <div class="campos">
          @if (datos.establecimiento; as e) {
            <div class="red-fija ancho-completo">
              <span class="atenuado">Red</span>
              <span>{{ e.sedeNombre }}</span>
              <span class="atenuado nota">Un establecimiento no cambia de red.</span>
            </div>
          } @else {
            <mat-form-field subscriptSizing="dynamic" class="ancho-completo">
              <mat-label>Red</mat-label>
              <mat-select formControlName="sedeId">
                @for (sede of datos.sedes; track sede.id) {
                  <mat-option [value]="sede.id">{{ sede.nombre }}</mat-option>
                }
              </mat-select>
              @if (formulario.controls.sedeId.hasError('required')) {
                <mat-error>Elija la red.</mat-error>
              }
            </mat-form-field>
          }

          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Código único</mat-label>
            <input matInput formControlName="codigoUnico" inputmode="numeric" class="mono" autocomplete="off" />
            @if (formulario.controls.codigoUnico.invalid) {
              <mat-error>Escriba un número entero, sin letras ni espacios.</mat-error>
            }
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Id_Establecimiento</mat-label>
            <input matInput formControlName="idEstablecimiento" inputmode="numeric" class="mono" autocomplete="off" />
            @if (formulario.controls.idEstablecimiento.invalid) {
              <mat-error>Escriba un número entero, sin letras ni espacios.</mat-error>
            }
          </mat-form-field>

          <mat-form-field subscriptSizing="dynamic" class="ancho-completo">
            <mat-label>Nombre</mat-label>
            <input matInput formControlName="nombre" maxlength="200" />
            @if (formulario.controls.nombre.hasError('required')) {
              <mat-error>Escriba el nombre.</mat-error>
            }
          </mat-form-field>

          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Categoría</mat-label>
            <input matInput formControlName="categoria" maxlength="20" placeholder="I-2" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Microred</mat-label>
            <input matInput formControlName="microred" maxlength="150" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Provincia</mat-label>
            <input matInput formControlName="provincia" maxlength="100" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Distrito</mat-label>
            <input matInput formControlName="distrito" maxlength="100" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Ubigeo</mat-label>
            <input matInput formControlName="ubigeo" inputmode="numeric" maxlength="6" class="mono" placeholder="050601" autocomplete="off" />
            @if (formulario.controls.ubigeo.hasError('pattern')) {
              <mat-error>6 dígitos, por ejemplo 050601.</mat-error>
            }
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="guardando()">{{ guardando() ? 'Guardando…' : 'Guardar' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .campos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; padding-top: 8px; }
    .ancho-completo { grid-column: 1 / -1; }
    mat-form-field { width: 100%; }
    app-aviso { display: block; margin-bottom: 16px; }
    .red-fija { display: flex; flex-direction: column; gap: 2px; }
    .red-fija .nota { font-size: 12px; }
    @media (max-width: 560px) { .campos { grid-template-columns: 1fr; } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstablecimientoDialog {
  protected readonly datos = inject<DatosEstablecimientoDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<EstablecimientoDialog, boolean>>(MatDialogRef);

  private readonly actual = this.datos.establecimiento;

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    sedeId: [
      { value: this.actual?.sedeId ?? this.sedeInicial(), disabled: this.actual !== null },
      Validators.required,
    ],
    codigoUnico: [this.actual ? String(this.actual.codigoUnico) : '', [Validators.required, Validators.pattern(NUMERO)]],
    idEstablecimiento: [this.actual ? String(this.actual.idEstablecimiento) : '', [Validators.required, Validators.pattern(NUMERO)]],
    nombre: [this.actual?.nombre ?? '', [Validators.required, Validators.maxLength(200)]],
    categoria: [this.actual?.categoria ?? ''],
    microred: [this.actual?.microred ?? ''],
    provincia: [this.actual?.provincia ?? ''],
    distrito: [this.actual?.distrito ?? ''],
    ubigeo: [this.actual?.ubigeo ?? '', Validators.pattern(UBIGEO)],
  });

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected async guardar(): Promise<void> {
    if (this.formulario.invalid || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);

    const v = this.formulario.getRawValue();
    const opcional = (texto: string) => (texto.trim() === '' ? null : texto.trim());
    const cuerpo = {
      idEstablecimiento: Number(v.idEstablecimiento),
      codigoUnico: Number(v.codigoUnico),
      nombre: v.nombre.trim(),
      categoria: opcional(v.categoria),
      microred: opcional(v.microred),
      provincia: opcional(v.provincia),
      distrito: opcional(v.distrito),
      ubigeo: opcional(v.ubigeo),
    };

    try {
      if (this.actual) {
        await this.api.invoke(editarEstablecimiento, { id: this.actual.id, body: cuerpo });
      } else {
        await this.api.invoke(crearEstablecimiento, { body: { ...cuerpo, sedeId: v.sedeId } });
      }
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }

  /** La red del selector del encabezado, si está entre las que se pueden elegir. */
  private sedeInicial(): string {
    const elegida = this.datos.sedeId;
    return elegida && this.datos.sedes.some((s) => s.id === elegida) ? elegida : '';
  }
}
