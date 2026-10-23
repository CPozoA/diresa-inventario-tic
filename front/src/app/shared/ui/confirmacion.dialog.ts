import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';

export interface DatosConfirmacion {
  readonly titulo: string;
  readonly mensaje: string;
  readonly confirmar: string;
  /** Acción destructiva o que corta el acceso: el botón se pinta en rojo. */
  readonly peligro?: boolean;
}

/** Pregunta antes de una acción importante. Devuelve verdadero solo si se confirma. */
@Component({
  selector: 'app-confirmacion',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ datos.titulo }}</h2>
    <mat-dialog-content>
      <p class="mensaje">{{ datos.mensaje }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="button" [class.boton-peligro]="datos.peligro" [mat-dialog-close]="true">
        {{ datos.confirmar }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .mensaje { margin: 0; line-height: 1.55; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmacionDialog {
  protected readonly datos = inject<DatosConfirmacion>(MAT_DIALOG_DATA);
}

export async function confirmar(dialogos: MatDialog, datos: DatosConfirmacion): Promise<boolean> {
  const referencia = dialogos.open(ConfirmacionDialog, { data: datos, width: '440px', autoFocus: 'dialog' });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}
