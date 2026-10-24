import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';

import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { PaginadorEnEspanol } from '../../../shared/ui/paginador';
import { DatosEstablecimientoDialog, EstablecimientoDialog } from '../components/establecimiento.dialog';
import { ImportarEstablecimientosDialog, ResultadoImportarDialog } from '../components/importar-establecimientos.dialog';
import { EstablecimientosStore } from '../data/establecimientos.store';
import { FiltroEstado, filtrarEstablecimientos } from '../data/filtro-establecimientos';

/**
 * Establecimientos de salud (docs/ARQUITECTURA.md §5.9). Solo el superadministrador: con "Todas las sedes" ve todos,
 * con una sede elegida en el encabezado, solo los de esa red.
 */
@Component({
  selector: 'app-establecimientos',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    AvisoComponent,
  ],
  providers: [EstablecimientosStore, { provide: MatPaginatorIntl, useClass: PaginadorEnEspanol }],
  templateUrl: './establecimientos.page.html',
  styleUrl: './establecimientos.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstablecimientosPage {
  protected readonly store = inject(EstablecimientosStore);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  protected readonly tamanosPagina = [25, 50, 100];
  protected readonly busqueda = signal('');
  protected readonly filtroEstado = signal<FiltroEstado>('activos');
  protected readonly pagina = signal(0);
  protected readonly tamanoPagina = signal(50);

  /** Con "Todas las sedes" se muestra la red de cada establecimiento. */
  protected readonly muestraRed = computed(() => this.sedeSeleccionada.sedeId() === null);

  protected readonly ambito = computed(() => this.sedeSeleccionada.sede()?.nombre ?? 'Todas las sedes');

  protected readonly filtrados = computed(() =>
    filtrarEstablecimientos(this.store.establecimientos(), this.busqueda(), this.filtroEstado()),
  );

  protected readonly visibles = computed(() => {
    const inicio = this.pagina() * this.tamanoPagina();
    return this.filtrados().slice(inicio, inicio + this.tamanoPagina());
  });

  constructor() {
    effect(() => {
      const sedeId = this.sedeSeleccionada.sedeId();
      untracked(() => void this.store.cargar(sedeId));
    });

    // Al cambiar la búsqueda, el estado o la sede, se vuelve a la primera página.
    effect(() => {
      this.busqueda();
      this.filtroEstado();
      this.sedeSeleccionada.sedeId();
      untracked(() => this.pagina.set(0));
    });
  }

  protected cambiarPagina(evento: PageEvent): void {
    this.pagina.set(evento.pageIndex);
    this.tamanoPagina.set(evento.pageSize);
  }

  protected ubicacion(e: EstablecimientoDto): string {
    return [e.distrito, e.provincia].filter((x) => !!x).join(', ');
  }

  protected async nuevo(): Promise<void> {
    await this.abrirDialogo({
      establecimiento: null,
      sedes: this.sesion.esSuperAdmin() ? this.sedesActivas() : [],
      sedeId: this.sedeSeleccionada.sedeId(),
    });
  }

  protected async editar(establecimiento: EstablecimientoDto): Promise<void> {
    await this.abrirDialogo({ establecimiento, sedes: [], sedeId: establecimiento.sedeId });
  }

  protected async importar(): Promise<void> {
    const resultado = await firstValueFrom(
      this.dialogos
        .open<ImportarEstablecimientosDialog, void, ResultadoImportarDialog>(ImportarEstablecimientosDialog, {
          width: '640px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );

    if (resultado) {
      this.notificaciones.exito(resultado.mensaje);
      await this.store.refrescar();
    }
  }

  protected async cambiarEstado(establecimiento: EstablecimientoDto): Promise<void> {
    const activar = !establecimiento.activo;

    if (!activar) {
      const confirmado = await confirmar(this.dialogos, {
        titulo: `Desactivar «${establecimiento.nombre}»`,
        mensaje:
          'Dejará de ofrecerse al registrar activos, pero se conserva en los activos que ya lo usan. Puede activarlo de nuevo cuando lo necesite.',
        confirmar: 'Desactivar',
      });
      if (!confirmado) {
        return;
      }
    }

    try {
      await this.store.cambiarEstado(establecimiento, activar);
      this.notificaciones.exito(
        activar ? `«${establecimiento.nombre}» está activo otra vez.` : `«${establecimiento.nombre}» ya no se ofrecerá al registrar.`,
      );
    } catch (error: unknown) {
      this.notificaciones.error(error);
    }
  }

  private sedesActivas() {
    return this.sedeSeleccionada.sedes().filter((s) => s.activa);
  }

  private async abrirDialogo(datos: DatosEstablecimientoDialog): Promise<void> {
    const guardado = await firstValueFrom(
      this.dialogos
        .open<EstablecimientoDialog, DatosEstablecimientoDialog, boolean>(EstablecimientoDialog, {
          data: datos,
          width: '640px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );

    if (guardado) {
      this.notificaciones.exito(datos.establecimiento ? 'Establecimiento actualizado.' : 'Establecimiento registrado.');
      await this.store.refrescar();
    }
  }
}
