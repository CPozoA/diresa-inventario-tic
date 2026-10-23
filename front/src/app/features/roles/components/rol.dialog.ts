import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { Api } from '../../../api/api';
import { crearRol } from '../../../api/fn/roles/crear-rol';
import { editarRol } from '../../../api/fn/roles/editar-rol';
import { PermisoDto } from '../../../api/models/permiso-dto';
import { RolDto } from '../../../api/models/rol-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';

export interface DatosRolDialog {
  /** Nulo para crear un rol nuevo. */
  readonly rol: RolDto | null;
  readonly permisos: readonly PermisoDto[];
  /** Sede donde se crea (solo la indica el superadministrador). */
  readonly sedeId: string | null;
  /** Ver sin editar: roles del sistema o usuarios sin Roles.Gestionar. */
  readonly soloLectura: boolean;
}

interface GrupoPermisos {
  readonly modulo: string;
  readonly permisos: readonly PermisoDto[];
}

/**
 * Crear, editar o ver un rol: nombre, descripción y permisos agrupados por módulo.
 * Los permisos que quien edita no tiene aparecen deshabilitados (la API los rechazaría).
 */
@Component({
  selector: 'app-rol-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    AvisoComponent,
  ],
  templateUrl: './rol.dialog.html',
  styleUrl: './rol.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolDialog {
  protected readonly datos = inject<DatosRolDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<RolDialog, boolean>>(MatDialogRef);

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    nombre: [this.datos.rol?.nombre ?? '', [Validators.required, Validators.maxLength(60)]],
    descripcion: [this.datos.rol?.descripcion ?? '', Validators.maxLength(250)],
  });

  protected readonly seleccion = signal(new Set(this.datos.rol?.permisos ?? []));
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected readonly titulo = this.datos.soloLectura
    ? (this.datos.rol?.nombre ?? 'Rol')
    : this.datos.rol
      ? 'Editar rol'
      : 'Nuevo rol';

  protected readonly grupos: GrupoPermisos[] = agrupar(this.datos.permisos);
  private readonly otorgables = new Set(this.datos.permisos.filter((p) => p.puedeOtorgar).map((p) => p.codigo));

  /** El rol ya tiene permisos que quien edita no tiene: la API no le dejará modificarlo. */
  protected readonly conPermisosAjenos =
    !this.datos.soloLectura && (this.datos.rol?.permisos ?? []).some((p) => !this.otorgables.has(p));

  protected readonly editable = !this.datos.soloLectura && !this.conPermisosAjenos;
  protected readonly cantidad = computed(() => this.seleccion().size);

  constructor() {
    if (!this.editable) {
      this.formulario.disable();
    }
  }

  protected deshabilitado(permiso: PermisoDto): boolean {
    return !this.editable || !permiso.puedeOtorgar;
  }

  protected alternar(codigo: string, marcado: boolean): void {
    const nueva = new Set(this.seleccion());
    if (marcado) {
      nueva.add(codigo);
    } else {
      nueva.delete(codigo);
    }
    this.seleccion.set(nueva);
  }

  protected async guardar(): Promise<void> {
    if (!this.editable || this.formulario.invalid || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.guardando.set(true);
    this.error.set(null);

    try {
      const { nombre, descripcion } = this.formulario.getRawValue();
      const permisos = [...this.seleccion()];
      const desc = descripcion.trim() === '' ? null : descripcion;

      if (this.datos.rol) {
        await this.api.invoke(editarRol, { id: this.datos.rol.id, body: { nombre, descripcion: desc, permisos } });
      } else {
        await this.api.invoke(crearRol, { body: { sedeId: this.datos.sedeId, nombre, descripcion: desc, permisos } });
      }
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}

function agrupar(permisos: readonly PermisoDto[]): GrupoPermisos[] {
  const grupos = new Map<string, PermisoDto[]>();
  for (const permiso of permisos) {
    const lista = grupos.get(permiso.modulo) ?? [];
    lista.push(permiso);
    grupos.set(permiso.modulo, lista);
  }
  return [...grupos].map(([modulo, lista]) => ({ modulo, permisos: lista }));
}
