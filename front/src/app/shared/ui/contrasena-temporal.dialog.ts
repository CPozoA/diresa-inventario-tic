import { Clipboard } from '@angular/cdk/clipboard';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

import { AvisoComponent } from './aviso.component';

export interface DatosContrasenaTemporal {
  readonly titulo: string;
  readonly nombreUsuario: string;
  readonly contrasena: string;
}

/**
 * Muestra la contraseña temporal que generó la API. Es la única vez que se ve: no se guarda en ningún lado
 * y el usuario deberá cambiarla al ingresar.
 */
@Component({
  selector: 'app-contrasena-temporal',
  imports: [MatDialogModule, MatButtonModule, MatIconModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>{{ datos.titulo }}</h2>
    <mat-dialog-content class="contenido">
      <app-aviso tono="alerta">Anótela ahora: no se volverá a mostrar.</app-aviso>
      <dl>
        <dt>Usuario</dt>
        <dd class="mono">{{ datos.nombreUsuario }}</dd>
        <dt>Contraseña temporal</dt>
        <dd class="clave">
          <span class="mono">{{ datos.contrasena }}</span>
          <button mat-stroked-button type="button" (click)="copiar()">
            <mat-icon>{{ copiada() ? 'check' : 'content_copy' }}</mat-icon>
            {{ copiada() ? 'Copiada' : 'Copiar' }}
          </button>
        </dd>
      </dl>
      <p class="atenuado">
        Entréguela a la persona por un medio seguro (en persona o por teléfono). Al ingresar, el sistema le pedirá
        crear su propia contraseña.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button type="button" mat-dialog-close>Ya la anoté</button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; }
    dl { margin: 0; display: grid; grid-template-columns: max-content 1fr; gap: 10px 16px; align-items: center; }
    dt { color: var(--inv-atenuado); }
    dd { margin: 0; }
    .clave { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .clave .mono { font-size: 20px; letter-spacing: 0.04em; padding: 6px 10px; background: var(--inv-fondo); border-radius: 6px; }
    p { margin: 0; line-height: 1.5; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContrasenaTemporalDialog {
  protected readonly datos = inject<DatosContrasenaTemporal>(MAT_DIALOG_DATA);
  private readonly portapapeles = inject(Clipboard);
  protected readonly copiada = signal(false);

  protected copiar(): void {
    this.copiada.set(this.portapapeles.copy(this.datos.contrasena));
  }
}
