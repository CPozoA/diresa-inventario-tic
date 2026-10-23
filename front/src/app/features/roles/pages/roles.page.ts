import { ChangeDetectionStrategy, Component, computed, effect, inject, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';

import { RolDto } from '../../../api/models/rol-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { DatosRolDialog, RolDialog } from '../components/rol.dialog';
import { RolesStore } from '../data/roles.store';

/** Roles de la sede. El superadministrador elige la sede en el encabezado. */
@Component({
  selector: 'app-roles',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTooltipModule,
    AvisoComponent,
    SiTienePermisoDirective,
  ],
  providers: [RolesStore],
  templateUrl: './roles.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolesPage {
  protected readonly store = inject(RolesStore);
  protected readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  protected readonly permisos = Permisos;
  protected readonly puedeGestionar = computed(() => this.sesion.tienePermiso(Permisos.Roles.Gestionar));

  /** El superadministrador debe elegir una sede: los roles son de cada sede. */
  protected readonly faltaSede = computed(() => this.sesion.esSuperAdmin() && this.sedeSeleccionada.sedeId() === null);

  protected readonly ambito = computed(() =>
    this.sesion.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? '') : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );

  constructor() {
    effect(() => {
      const faltaSede = this.faltaSede();
      const sedeId = this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null;
      untracked(() => (faltaSede ? this.store.limpiar() : void this.store.cargar(sedeId)));
    });
  }

  protected cantidadUsuarios(rol: RolDto): number {
    return Number(rol.cantidadUsuarios);
  }

  protected async abrir(rol: RolDto | null): Promise<void> {
    const datos: DatosRolDialog = {
      rol,
      permisos: this.store.permisos(),
      sedeId: this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null,
      soloLectura: !this.puedeGestionar() || rol?.esDelSistema === true,
    };

    const guardado = await firstValueFrom(
      this.dialogos
        .open<RolDialog, DatosRolDialog, boolean>(RolDialog, { data: datos, width: '760px', maxWidth: '95vw' })
        .afterClosed(),
    );

    if (guardado) {
      this.notificaciones.exito(rol ? 'Rol actualizado.' : 'Rol creado.');
      await this.store.recargar();
    }
  }

  protected async eliminar(rol: RolDto): Promise<void> {
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Eliminar el rol ${rol.nombre}`,
      mensaje: 'Esta acción no se puede deshacer.',
      confirmar: 'Eliminar',
      peligro: true,
    });

    if (!confirmado) {
      return;
    }

    try {
      await this.store.eliminar(rol);
      this.notificaciones.exito('Rol eliminado.');
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }
}
