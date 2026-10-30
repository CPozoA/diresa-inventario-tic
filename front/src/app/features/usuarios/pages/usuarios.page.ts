import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';

import { UsuarioDto } from '../../../api/models/usuario-dto';
import { Permisos } from '../../../core/auth/permisos';
import { motivoNoGestionable } from '../../../core/auth/reglas-gestion';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { ContrasenaTemporalDialog, DatosContrasenaTemporal } from '../../../shared/ui/contrasena-temporal.dialog';
import { DatosRolesUsuarioDialog, RolesUsuarioDialog } from '../components/roles-usuario.dialog';
import { DatosUsuarioDialog, ResultadoUsuarioDialog, UsuarioDialog } from '../components/usuario.dialog';
import { UsuariosStore } from '../data/usuarios.store';

type FiltroEstado = 'todos' | 'activos' | 'inactivos';

interface FilaUsuario {
  readonly usuario: UsuarioDto;
  readonly nombreCompleto: string;
  readonly sede: string;
  /** Motivo por el que no se puede gestionar (la API lo rechazaría), o nulo. */
  readonly bloqueoGestion: string | null;
}

/** Usuarios de la sede: lista con búsqueda y las acciones de gestión según los permisos de quien consulta. */
@Component({
  selector: 'app-usuarios',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    AvisoComponent,
    SiTienePermisoDirective,
  ],
  providers: [UsuariosStore],
  templateUrl: './usuarios.page.html',
  styleUrl: './usuarios.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsuariosPage {
  protected readonly store = inject(UsuariosStore);
  protected readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  protected readonly permisos = Permisos;
  protected readonly puedeGestionar = computed(() => this.sesion.tienePermiso(Permisos.Usuarios.Gestionar));
  protected readonly busqueda = signal('');
  protected readonly filtroEstado = signal<FiltroEstado>('activos');

  /** El superadministrador con "Todas las sedes" ve una columna con la sede de cada usuario. */
  protected readonly muestraSede = computed(() => this.sesion.esSuperAdmin() && this.sedeSeleccionada.sedeId() === null);

  protected readonly ambito = computed(() =>
    this.sesion.esSuperAdmin()
      ? (this.sedeSeleccionada.sede()?.nombre ?? 'Todas las sedes')
      : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );

  protected readonly filas = computed<FilaUsuario[]>(() => {
    const texto = normalizar(this.busqueda());
    const estado = this.filtroEstado();
    const sedes = new Map(this.sedeSeleccionada.sedes().map((s) => [s.id, s.nombre]));
    const actual = {
      usuarioId: this.sesion.sesion()?.usuarioId ?? null,
      esSuperAdmin: this.sesion.esSuperAdmin(),
      permisos: this.sesion.permisos(),
    };

    return this.store
      .usuarios()
      .filter((u) => estado === 'todos' || (estado === 'activos') === u.activo)
      .map((u) => ({
        usuario: u,
        nombreCompleto: `${u.nombres} ${u.apellidos}`,
        sede: sedes.get(u.sedeId) ?? '',
        bloqueoGestion: motivoNoGestionable(u, this.store.roles(), actual),
      }))
      .filter(
        (f) =>
          texto === '' ||
          [f.nombreCompleto, f.usuario.dni ?? '', f.usuario.nombreUsuario, f.usuario.correo ?? '', f.usuario.cargo ?? '', f.sede].some((c) =>
            normalizar(c).includes(texto),
          ),
      );
  });

  constructor() {
    effect(() => {
      const sedeId = this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null;
      const esSuperAdmin = this.sesion.esSuperAdmin();
      untracked(() => void this.store.cargar(sedeId, esSuperAdmin));
    });
  }

  protected async nuevo(): Promise<void> {
    const datos: DatosUsuarioDialog = {
      usuario: null,
      esSuperAdmin: this.sesion.esSuperAdmin(),
      sedeId: this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null,
      sedes: this.sedeSeleccionada.sedes().filter((s) => s.activa),
    };

    const resultado = await this.abrirDialogoUsuario(datos);
    if (resultado && resultado !== true) {
      this.mostrarContrasena({
        titulo: 'Usuario creado',
        nombreUsuario: resultado.nombreUsuario,
        contrasena: resultado.contrasenaTemporal,
      });
      await this.store.refrescar();
    }
  }

  protected async editar(usuario: UsuarioDto): Promise<void> {
    const resultado = await this.abrirDialogoUsuario({
      usuario,
      esSuperAdmin: this.sesion.esSuperAdmin(),
      sedeId: usuario.sedeId,
      sedes: [],
    });

    if (resultado) {
      this.notificaciones.exito('Datos actualizados.');
      await this.store.refrescar();
    }
  }

  protected async cambiarRoles(usuario: UsuarioDto): Promise<void> {
    const guardado = await firstValueFrom(
      this.dialogos
        .open<RolesUsuarioDialog, DatosRolesUsuarioDialog, boolean>(RolesUsuarioDialog, {
          data: { usuario, esSuperAdmin: this.sesion.esSuperAdmin() },
        })
        .afterClosed(),
    );

    if (guardado) {
      this.notificaciones.exito('Roles actualizados.');
      await this.store.refrescar();
    }
  }

  protected async restablecer(usuario: UsuarioDto): Promise<void> {
    const confirmado = await confirmar(this.dialogos, {
      titulo: 'Restablecer contraseña',
      mensaje: `Se generará una contraseña temporal para ${usuario.nombreUsuario}, se quitará cualquier bloqueo y se cerrarán sus sesiones abiertas.`,
      confirmar: 'Restablecer',
    });

    if (!confirmado) {
      return;
    }

    try {
      const contrasena = await this.store.restablecerContrasena(usuario);
      this.mostrarContrasena({ titulo: 'Contraseña restablecida', nombreUsuario: usuario.nombreUsuario, contrasena });
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }

  protected async desbloquear(usuario: UsuarioDto): Promise<void> {
    try {
      await this.store.desbloquear(usuario);
      this.notificaciones.exito(`${usuario.nombreUsuario} ya puede volver a intentar ingresar.`);
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }

  protected async cambiarEstado(usuario: UsuarioDto): Promise<void> {
    const activar = !usuario.activo;

    if (!activar) {
      const confirmado = await confirmar(this.dialogos, {
        titulo: `Desactivar a ${usuario.nombres} ${usuario.apellidos}`,
        mensaje: 'Conserva su historial, pero ya no podrá ingresar y se cerrarán sus sesiones abiertas.',
        confirmar: 'Desactivar',
        peligro: true,
      });
      if (!confirmado) {
        return;
      }
    }

    try {
      await this.store.cambiarEstado(usuario, activar);
      this.notificaciones.exito(activar ? 'Usuario activado.' : 'Usuario desactivado.');
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }

  private async abrirDialogoUsuario(datos: DatosUsuarioDialog): Promise<ResultadoUsuarioDialog | undefined> {
    return firstValueFrom(
      this.dialogos
        .open<UsuarioDialog, DatosUsuarioDialog, ResultadoUsuarioDialog>(UsuarioDialog, {
          data: datos,
          width: '640px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
  }

  private mostrarContrasena(datos: DatosContrasenaTemporal): void {
    this.dialogos.open(ContrasenaTemporalDialog, { data: datos, width: '520px', maxWidth: '95vw', disableClose: true });
  }
}

/** Minúsculas y sin tildes, para que "maria" encuentre a "María". */
function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim();
}
