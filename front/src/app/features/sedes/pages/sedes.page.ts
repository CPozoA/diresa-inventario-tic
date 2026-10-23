import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';

import { SedeDto } from '../../../api/models/sede-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { SedeDialog } from '../components/sede.dialog';
import { SedesStore } from '../data/sedes.store';

/** Sedes del sistema. Solo para el superadministrador (permiso Sedes.Gestionar). */
@Component({
  selector: 'app-sedes',
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule, AvisoComponent],
  providers: [SedesStore],
  templateUrl: './sedes.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SedesPage {
  protected readonly store = inject(SedesStore);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  constructor() {
    void this.store.cargar();
  }

  protected async abrir(sede: SedeDto | null): Promise<void> {
    const guardado = await firstValueFrom(
      this.dialogos.open<SedeDialog, SedeDto | null, boolean>(SedeDialog, { data: sede }).afterClosed(),
    );

    if (guardado) {
      this.notificaciones.exito(sede ? 'Sede actualizada.' : 'Sede creada con los roles Editor y Consulta.');
      await this.store.despuesDeCambiar();
    }
  }

  protected async cambiarEstado(sede: SedeDto): Promise<void> {
    const activar = !sede.activa;
    const confirmado = await confirmar(this.dialogos, activar
      ? { titulo: `Activar ${sede.nombre}`, mensaje: 'Sus usuarios activos podrán volver a ingresar.', confirmar: 'Activar' }
      : {
          titulo: `Desactivar ${sede.nombre}`,
          mensaje: 'La sede conserva sus datos, pero sus usuarios ya no podrán ingresar y se cerrarán sus sesiones abiertas.',
          confirmar: 'Desactivar',
          peligro: true,
        });

    if (!confirmado) {
      return;
    }

    try {
      await this.store.cambiarEstado(sede, activar);
      this.notificaciones.exito(activar ? 'Sede activada.' : 'Sede desactivada.');
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }
}
