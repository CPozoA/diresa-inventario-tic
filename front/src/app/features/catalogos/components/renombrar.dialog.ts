import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { Api } from '../../../api/api';
import { renombrarElementoCatalogo } from '../../../api/fn/catalogos/renombrar-elemento-catalogo';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';

export interface DatosRenombrar {
  readonly tipo: string;
  readonly nombreSingular: string;
  readonly elemento: ElementoCatalogoDto;
}

/** Cambia el nombre de un elemento de catálogo. */
@Component({
  selector: 'app-renombrar-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>Renombrar {{ datos.nombreSingular.toLowerCase() }}</h2>
    <form [formGroup]="formulario" (ngSubmit)="guardar()" novalidate>
      <mat-dialog-content class="contenido">
        @if (error(); as e) {
          <app-aviso>{{ e.mensaje }}</app-aviso>
        }
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Nombre</mat-label>
          <input matInput formControlName="nombre" maxlength="100" />
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
    .contenido { display: flex; flex-direction: column; gap: 16px; min-width: min(380px, 80vw); }
    mat-form-field { width: 100%; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RenombrarDialog {
  protected readonly datos = inject<DatosRenombrar>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<RenombrarDialog, boolean>>(MatDialogRef);

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    nombre: [this.datos.elemento.nombre, [Validators.required, Validators.maxLength(100)]],
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
      await this.api.invoke(renombrarElementoCatalogo, {
        tipo: this.datos.tipo,
        id: this.datos.elemento.id,
        body: { nombre: this.formulario.getRawValue().nombre },
      });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
