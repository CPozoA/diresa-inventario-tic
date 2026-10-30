import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { Api } from '../../../api/api';
import { listarRoles } from '../../../api/fn/roles/listar-roles';
import { consultarDniUsuario } from '../../../api/fn/usuarios/consultar-dni-usuario';
import { crearUsuario } from '../../../api/fn/usuarios/crear-usuario';
import { editarUsuario } from '../../../api/fn/usuarios/editar-usuario';
import { ConsultaDniUsuarioDto } from '../../../api/models/consulta-dni-usuario-dto';
import { SedeDto } from '../../../api/models/sede-dto';
import { UsuarioCreadoDto } from '../../../api/models/usuario-creado-dto';
import { UsuarioDto } from '../../../api/models/usuario-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { esDniValido, limpiarDni } from '../../asignaciones/data/personal';
import { EstadoDniUsuario, estadoDniUsuario, mensajeDniUsuario, nombrePropio } from '../data/dni-usuario';
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

/**
 * Crear un usuario (DNI, datos, sede y roles) o editar sus datos y correo. El nombre de usuario no cambia.
 * Al crear se busca primero el DNI: si ya es de otro usuario se avisa; si RENIEC lo encuentra, llena nombres y
 * apellidos (se pueden corregir). Los usuarios creados antes de que se pidiera el DNI lo completan al editarlos (D57).
 */
@Component({
  selector: 'app-usuario-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
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
  /** El DNI es obligatorio al crear y en quien ya lo tiene; un usuario anterior puede seguir sin él. */
  protected readonly dniObligatorio = this.creando || !!this.datos.usuario?.dni;

  protected readonly formulario = inject(NonNullableFormBuilder).group({
    dni: [this.datos.usuario?.dni ?? '', [...(this.dniObligatorio ? [Validators.required] : []), Validators.pattern(/^\d{8}$/)]],
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

  /** Búsqueda del DNI. Al crear es obligatoria antes de guardar; al editar, solo si se quiere completar o corregir. */
  protected readonly estadoDni = signal<EstadoDniUsuario>('pendiente');
  protected readonly consulta = signal<ConsultaDniUsuarioDto | null>(null);
  protected readonly mensajeDni = computed(() => mensajeDniUsuario(this.estadoDni()));
  /** Nombres y apellidos que se llenaron con la última búsqueda en RENIEC. */
  private llenadosDeReniec: { nombres: string; apellidos: string } | null = null;

  /** Al crear, el botón principal busca el DNI mientras no se haya buscado. */
  protected readonly debeBuscar = computed(() => this.creando && this.estadoDni() === 'pendiente');

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

  constructor() {
    // Cambiar el DNI invalida la búsqueda anterior.
    this.formulario.controls.dni.valueChanges.pipe(takeUntilDestroyed()).subscribe((valor) => {
      const limpio = limpiarDni(valor);
      if (limpio !== valor) {
        this.formulario.controls.dni.setValue(limpio, { emitEvent: false });
      }
      if (this.estadoDni() !== 'pendiente' && limpio !== this.consulta()?.dni) {
        this.estadoDni.set('pendiente');
        this.consulta.set(null);
      }
    });
  }

  protected async buscarDni(evento?: Event): Promise<void> {
    evento?.preventDefault();
    const dni = this.formulario.controls.dni.value;
    if (!esDniValido(dni) || this.estadoDni() === 'consultando') {
      this.formulario.controls.dni.markAsTouched();
      return;
    }

    this.estadoDni.set('consultando');
    this.error.set(null);
    try {
      const consulta = await this.api.invoke(consultarDniUsuario, {
        body: { dni, excluirUsuarioId: this.datos.usuario?.id ?? null },
      });
      this.consulta.set(consulta);
      const estado = estadoDniUsuario(consulta);
      this.estadoDni.set(estado);
      if (estado === 'encontrado') {
        const llenados = { nombres: nombrePropio(consulta.nombres ?? ''), apellidos: nombrePropio(consulta.apellidos ?? '') };
        this.formulario.patchValue(llenados);
        this.llenadosDeReniec = llenados;
      } else {
        this.quitarNombresDeOtroDni();
      }
    } catch (error: unknown) {
      this.estadoDni.set('pendiente');
      this.error.set(aErrorApi(error));
    }
  }

  /** Si los nombres siguen siendo los que RENIEC dio para otro DNI, se borran: no son de esta persona. */
  private quitarNombresDeOtroDni(): void {
    const v = this.formulario.getRawValue();
    if (this.llenadosDeReniec && v.nombres === this.llenadosDeReniec.nombres && v.apellidos === this.llenadosDeReniec.apellidos) {
      this.formulario.patchValue({ nombres: '', apellidos: '' });
      this.formulario.controls.nombres.markAsUntouched();
      this.formulario.controls.apellidos.markAsUntouched();
    }
    this.llenadosDeReniec = null;
  }

  protected cambioDeSede(): void {
    this.rolIds.set([]);
  }

  protected async guardar(): Promise<void> {
    if (this.guardando() || this.estadoDni() === 'consultando' || this.estadoDni() === 'registrado') {
      return;
    }

    // Al crear, primero se busca el DNI: así se ven los nombres de RENIEC antes de guardar.
    if (this.debeBuscar()) {
      await this.buscarDni();
      return;
    }

    if (this.formulario.invalid) {
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
    const dni = v.dni === '' ? null : v.dni;

    try {
      if (this.datos.usuario) {
        await this.api.invoke(editarUsuario, {
          id: this.datos.usuario.id,
          body: { nombres: v.nombres, apellidos: v.apellidos, cargo, correo: v.correo, dni },
        });
        this.referencia.close(true);
      } else {
        const sedeId = this.datos.esSuperAdmin ? v.sedeId || this.datos.sedeId : null;
        const creado = await this.api.invoke(crearUsuario, {
          body: {
            sedeId,
            nombreUsuario: v.nombreUsuario.trim(),
            correo: v.correo.trim(),
            dni,
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
