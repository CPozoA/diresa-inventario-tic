import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { Api } from '../../../api/api';
import { listarRoles } from '../../../api/fn/roles/listar-roles';
import { asignarRolesUsuario } from '../../../api/fn/usuarios/asignar-roles-usuario';
import { UsuarioDto } from '../../../api/models/usuario-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { SelectorRolesComponent } from './selector-roles.component';

export interface DatosRolesUsuarioDialog {
  readonly usuario: UsuarioDto;
  readonly esSuperAdmin: boolean;
}

/** Cambiar los roles de un usuario. Reemplaza todos sus roles por los marcados. */
@Component({
  selector: 'app-roles-usuario-dialog',
  imports: [MatButtonModule, MatDialogModule, MatProgressBarModule, AvisoComponent, SelectorRolesComponent],
  template: `
    <h2 mat-dialog-title>Roles de {{ datos.usuario.nombres }} {{ datos.usuario.apellidos }}</h2>
    <mat-dialog-content class="contenido">
      @if (error(); as e) {
        <app-aviso>{{ e.mensaje }}</app-aviso>
      }
      @if (roles.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      <app-selector-roles [roles]="listaRoles()" [(seleccion)]="rolIds" />
      <p class="atenuado">Los permisos del usuario son la suma de los permisos de todos sus roles.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="button" (click)="guardar()" [disabled]="guardando() || rolIds().length === 0">
        {{ guardando() ? 'Guardando…' : 'Guardar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; min-width: min(460px, 80vw); }
    p { margin: 0; font-size: 13px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolesUsuarioDialog {
  protected readonly datos = inject<DatosRolesUsuarioDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<RolesUsuarioDialog, boolean>>(MatDialogRef);

  protected readonly rolIds = signal(this.datos.usuario.roles.map((r) => r.id));

  protected readonly roles = resource({
    loader: () =>
      this.api.invoke(listarRoles, this.datos.esSuperAdmin ? { sedeId: this.datos.usuario.sedeId } : {}),
  });

  protected readonly listaRoles = computed(() => (this.roles.hasValue() ? this.roles.value() : []));
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected async guardar(): Promise<void> {
    this.guardando.set(true);
    this.error.set(null);

    try {
      await this.api.invoke(asignarRolesUsuario, { id: this.datos.usuario.id, body: { rolIds: this.rolIds() } });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
