import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, resource, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { Api } from '../../../api/api';
import { listarMovimientosAsignacion } from '../../../api/fn/asignaciones/listar-movimientos-asignacion';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { listarPersonal } from '../../../api/fn/personal/listar-personal';
import { MovimientoDto } from '../../../api/models/movimiento-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { formatoFecha, textoConservacion } from '../../activos/data/textos-activo';
import { SelectorCatalogoComponent } from '../../catalogos/components/selector-catalogo.component';
import { abrirPersonaDialog } from '../components/persona.dialog';
import {
  FILTROS_PERSONAL_INICIALES,
  FiltrosPersonal,
  VistaAsignaciones,
  filtrosPersonalDesdeParametros,
  hayFiltrosPersonal,
  iniciales,
  parametrosDesdeFiltrosPersonal,
  textoEquipos,
  textoMovimiento,
  tonoMovimiento,
} from '../data/personal';

const ESPERA_BUSQUEDA_MS = 350;
const MAXIMO_MOVIMIENTOS = 100;

/**
 * Asignaciones (D50, D51): el personal de la red con cuántos equipos tiene cada uno, y las entregas y devoluciones
 * recientes. Los filtros y la pestaña quedan en la dirección.
 */
@Component({
  selector: 'app-asignaciones',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTabsModule,
    AvisoComponent,
    SelectorCatalogoComponent,
    SiTienePermisoDirective,
  ],
  templateUrl: './asignaciones.page.html',
  styleUrl: './asignaciones.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AsignacionesPage {
  private readonly api = inject(Api);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly permisos = Permisos;
  protected readonly iniciales = iniciales;
  protected readonly textoEquipos = textoEquipos;
  protected readonly textoMovimiento = textoMovimiento;
  protected readonly tonoMovimiento = tonoMovimiento;
  protected readonly textoConservacion = textoConservacion;
  protected readonly formatoFecha = formatoFecha;

  protected readonly filtros = toSignal(
    this.ruta.queryParamMap.pipe(
      map((p) => filtrosPersonalDesdeParametros(Object.fromEntries(p.keys.map((k) => [k, p.get(k) ?? undefined])))),
    ),
    { requireSync: true },
  );

  protected readonly busqueda = new FormControl(this.filtros().buscar, { nonNullable: true });
  protected readonly filtroEstablecimiento = new FormControl<string | null>(this.filtros().establecimientoId);

  private readonly esSuperAdmin = this.sesion.esSuperAdmin;
  private readonly sedeId = computed(() => (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));

  /** Con "Todas las sedes" se muestra la red de cada persona y no se filtra por establecimiento. */
  protected readonly todasLasSedes = computed(() => this.esSuperAdmin() && this.sedeId() === null);

  protected readonly ambito = computed(() =>
    this.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? 'todas las redes') : (this.sesion.sesion()?.sede?.nombre ?? 'la red'),
  );

  protected readonly vista = computed(() => this.filtros().vista);
  protected readonly indicePestana = computed(() => (this.vista() === 'movimientos' ? 1 : 0));
  protected readonly conFiltros = computed(() => hayFiltrosPersonal(this.filtros()));

  protected readonly personal = resource({
    params: () => {
      const f = this.filtros();
      const sedeId = this.sedeId();
      return {
        ...(sedeId ? { sedeId } : {}),
        ...(f.buscar ? { buscar: f.buscar } : {}),
        ...(f.establecimientoId && !this.todasLasSedes() ? { establecimientoId: f.establecimientoId } : {}),
        incluirCesados: f.cesados,
      };
    },
    loader: ({ params }) => this.api.invoke(listarPersonal, params),
  });

  protected readonly movimientos = resource({
    params: () => (this.vista() === 'movimientos' ? { sedeId: this.sedeId(), maximo: MAXIMO_MOVIMIENTOS } : undefined),
    loader: ({ params }) =>
      this.api.invoke(listarMovimientosAsignacion, { maximo: params.maximo, ...(params.sedeId ? { sedeId: params.sedeId } : {}) }),
  });

  protected readonly establecimientos = resource({
    params: () => (this.todasLasSedes() ? undefined : { sedeId: this.sedeId() ?? undefined }),
    loader: ({ params }) => this.api.invoke(listarEstablecimientos, { ...params, incluirInactivos: true }),
  });

  protected readonly listaPersonal = computed(() => (this.personal.hasValue() ? this.personal.value() : []));
  protected readonly listaMovimientos = computed(() => (this.movimientos.hasValue() ? this.movimientos.value() : []));
  protected readonly errorPersonal = computed(() => (this.personal.error() ? aErrorApi(this.personal.error()).mensaje : null));
  protected readonly errorMovimientos = computed(() => (this.movimientos.error() ? aErrorApi(this.movimientos.error()).mensaje : null));

  constructor() {
    let sedeAnterior: string | null | undefined;

    effect(() => {
      const filtros = this.filtros();
      const sedeId = this.sedeId();
      untracked(() => {
        // Al cambiar de red, el establecimiento elegido ya no corresponde.
        if (sedeAnterior !== undefined && sedeAnterior !== sedeId && filtros.establecimientoId) {
          sedeAnterior = sedeId;
          this.aplicar({ ...filtros, establecimientoId: null });
          return;
        }
        sedeAnterior = sedeId;
        if (this.busqueda.value.trim() !== filtros.buscar) {
          this.busqueda.setValue(filtros.buscar, { emitEvent: false });
        }
        if (this.filtroEstablecimiento.value !== filtros.establecimientoId) {
          this.filtroEstablecimiento.setValue(filtros.establecimientoId, { emitEvent: false });
        }
      });
    });

    let espera: ReturnType<typeof setTimeout> | undefined;
    const porTexto = this.busqueda.valueChanges.subscribe((texto) => {
      clearTimeout(espera);
      espera = setTimeout(() => {
        if (texto.trim() !== this.filtros().buscar) {
          this.aplicar({ ...this.filtros(), buscar: texto });
        }
      }, ESPERA_BUSQUEDA_MS);
    });
    const porEstablecimiento = this.filtroEstablecimiento.valueChanges.subscribe((id) =>
      this.aplicar({ ...this.filtros(), establecimientoId: id }),
    );
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(espera);
      porTexto.unsubscribe();
      porEstablecimiento.unsubscribe();
    });
  }

  protected cambiarVista(indice: number): void {
    const vista: VistaAsignaciones = indice === 1 ? 'movimientos' : 'personal';
    if (vista !== this.vista()) {
      this.aplicar({ ...this.filtros(), vista });
    }
  }

  protected cambiarCesados(cesados: boolean): void {
    this.aplicar({ ...this.filtros(), cesados });
  }

  protected limpiarBusqueda(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...this.filtros(), buscar: '' });
  }

  protected quitarFiltros(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...FILTROS_PERSONAL_INICIALES, vista: this.vista() });
  }

  protected async nuevaPersona(): Promise<void> {
    const resultado = await abrirPersonaDialog(this.dialogos, { persona: null, sedeId: this.sedeId() });
    if (resultado) {
      this.notificaciones.exito('Persona registrada.');
      this.personal.reload();
    }
  }

  /** "Dell Latitude 5440 · Laptop". */
  protected equipo(m: MovimientoDto): string {
    return `${m.marca} ${m.modelo} · ${m.tipoActivo}`;
  }

  /** Estado de la dirección actual, para que la ficha pueda volver a esta misma lista. */
  protected volverAqui(): { volver: string } {
    return { volver: this.router.url };
  }

  private aplicar(filtros: FiltrosPersonal): void {
    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: parametrosDesdeFiltrosPersonal(filtros),
      replaceUrl: true,
    });
  }
}
