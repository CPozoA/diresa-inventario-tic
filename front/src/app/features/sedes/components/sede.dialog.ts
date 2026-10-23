import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { Api } from '../../../api/api';
import { crearSede } from '../../../api/fn/sedes/crear-sede';
import { editarSede } from '../../../api/fn/sedes/editar-sede';
import { SedeDto } from '../../../api/models/sede-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';

/** Crear una sede (código y nombre) o cambiar el nombre de una existente. El código no cambia. */
@Component({
  selector: 'app-sede-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>{{ sede ? 'Editar sede' : 'Nueva sede' }}</h2>
    <form [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
      <mat-dialog-content class="campos">
        @if (error(); as e) {
          <app-aviso>{{ e.mensaje }}</app-aviso>
        }
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Código</mat-label>
          <input matInput formControlName="codigo" class="mono" maxlength="20" autocapitalize="characters" />
          <mat-hint>{{ sede ? 'El código no se puede cambiar.' : 'Corto y sin espacios, por ejemplo HUANTA o SAN-MIGUEL.' }}</mat-hint>
          @if (formulario.controls.codigo.hasError('required')) {
            <mat-error>Escriba el código.</mat-error>
          } @else if (formulario.controls.codigo.hasError('pattern')) {
            <mat-error>De 2 a 20 caracteres: letras sin tilde, números y guiones.</mat-error>
          }
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="nombre" maxlength="150" />
          @if (formulario.controls.nombre.hasError('required')) {
            <mat-error>Escriba el nombre.</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="guardando()">{{ guardando() ? 'Guardando…' : 'Guardar' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .campos { display: flex; flex-direction: column; gap: 16px; min-width: min(420px, 80vw); }
    mat-form-field { width: 100%; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SedeDialog {
  protected readonly sede = inject<SedeDto | null>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<SedeDialog, boolean>>(MatDialogRef);

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    codigo: [
      { value: this.sede?.codigo ?? '', disabled: this.sede !== null },
      [Validators.required, Validators.pattern(/^[A-Za-z0-9-]{2,20}$/)],
    ],
    nombre: [this.sede?.nombre ?? '', Validators.required],
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

    try {
      const { codigo, nombre } = this.formulario.getRawValue();
      if (this.sede) {
        await this.api.invoke(editarSede, { id: this.sede.id, body: { nombre } });
      } else {
        await this.api.invoke(crearSede, { body: { codigo: codigo.toUpperCase(), nombre } });
      }
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
