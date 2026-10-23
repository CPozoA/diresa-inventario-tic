import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { Api } from '../../../api/api';
import { listarRoles } from '../../../api/fn/roles/listar-roles';
import { crearUsuario } from '../../../api/fn/usuarios/crear-usuario';
import { editarUsuario } from '../../../api/fn/usuarios/editar-usuario';
import { SedeDto } from '../../../api/models/sede-dto';
import { UsuarioCreadoDto } from '../../../api/models/usuario-creado-dto';
import { UsuarioDto } from '../../../api/models/usuario-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { SelectorRolesComponent } from './selector-roles.component';

export interface DatosUsuarioDialog {
  /** Nulo para crear un usuario nuevo. */
  readonly usuario: UsuarioDto | null;
  readonly esSuperAdmin: boolean;
  /** Sede elegida en el encabezado (superadministrador). Nula: se elige en el formulario. */
  readonly sedeId: string | null;
  readonly sedes: readonly SedeDto[];
}

/** Resultado al crear: el usuario y su contraseña temporal. Al editar: verdadero. */
export type ResultadoUsuarioDialog = (UsuarioCreadoDto & { nombreUsuario: string }) | true;

/** Crear un usuario (datos, sede y roles) o editar sus datos y correo. El nombre de usuario no cambia. */
@Component({
  selector: 'app-usuario-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    AvisoComponent,
    SelectorRolesComponent,
  ],
  templateUrl: './usuario.dialog.html',
  styleUrl: './usuario.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsuarioDialog {
  protected readonly datos = inject<DatosUsuarioDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<UsuarioDialog, ResultadoUsuarioDialog>>(MatDialogRef);

  protected readonly creando = this.datos.usuario === null;
  protected readonly eligeSede = this.creando && this.datos.esSuperAdmin && this.datos.sedeId === null;

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    nombreUsuario: [
      { value: this.datos.usuario?.nombreUsuario ?? '', disabled: !this.creando },
      [Validators.required, Validators.pattern(/^[A-Za-z0-9._@-]{3,50}$/)],
    ],
    correo: [this.datos.usuario?.correo ?? '', [Validators.required, Validators.email]],
    nombres: [this.datos.usuario?.nombres ?? '', [Validators.required, Validators.maxLength(100)]],
    apellidos: [this.datos.usuario?.apellidos ?? '', [Validators.required, Validators.maxLength(100)]],
    cargo: [this.datos.usuario?.cargo ?? '', Validators.maxLength(100)],
    sedeId: [this.datos.sedeId ?? '', this.eligeSede ? Validators.required : []],
  });

  protected readonly rolIds = signal<string[]>([]);

  private readonly sedeElegida = toSignal(this.formulario.controls.sedeId.valueChanges, {
    initialValue: this.formulario.controls.sedeId.value,
  });

  /** Roles de la sede donde se crea el usuario. */
  protected readonly roles = resource({
    params: () => (this.creando ? { sedeId: this.sedeElegida() } : undefined),
    loader: ({ params }) => {
      if (this.datos.esSuperAdmin && !params.sedeId) {
        return Promise.resolve([]);
      }
      return this.api.invoke(listarRoles, params.sedeId ? { sedeId: params.sedeId } : {});
    },
  });

  protected readonly listaRoles = computed(() => (this.roles.hasValue() ? this.roles.value() : []));
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected cambioDeSede(): void {
    this.rolIds.set([]);
  }

  protected async guardar(): Promise<void> {
    if (this.formulario.invalid || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    if (this.creando && this.rolIds().length === 0) {
      this.error.set({ estado: 400, codigo: null, mensaje: 'Marque al menos un rol.' });
      return;
    }

    this.guardando.set(true);
    this.error.set(null);

    const v = this.formulario.getRawValue();
    const cargo = v.cargo.trim() === '' ? null : v.cargo;

    try {
      if (this.datos.usuario) {
        await this.api.invoke(editarUsuario, {
          id: this.datos.usuario.id,
          body: { nombres: v.nombres, apellidos: v.apellidos, cargo, correo: v.correo },
        });
        this.referencia.close(true);
      } else {
        const sedeId = this.datos.esSuperAdmin ? v.sedeId || this.datos.sedeId : null;
        const creado = await this.api.invoke(crearUsuario, {
          body: {
            sedeId,
            nombreUsuario: v.nombreUsuario.trim(),
            correo: v.correo.trim(),
            nombres: v.nombres,
            apellidos: v.apellidos,
            cargo,
            rolIds: this.rolIds(),
          },
        });
        this.referencia.close({ ...creado, nombreUsuario: v.nombreUsuario.trim() });
      }
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
