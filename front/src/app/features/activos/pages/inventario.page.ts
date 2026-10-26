import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { LowerCasePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { Api } from '../../../api/api';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { ActivoResumenDto } from '../../../api/models/activo-resumen-dto';
import { EstadoGarantia } from '../../../api/models/estado-garantia';
import { SituacionRenovacion } from '../../../api/models/situacion-renovacion';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { SelectorCatalogoComponent } from '../../catalogos/components/selector-catalogo.component';
import { PaginadorEnEspanol } from '../../../shared/ui/paginador';
import {
  FiltrosInventario,
  TAMANOS_PAGINA,
  cambiarFiltro,
  filtrosDesdeParametros,
  hayFiltros,
  parametrosDesdeFiltros,
  sinFiltros,
} from '../data/filtros-inventario';
import { InventarioStore } from '../data/inventario.store';
import {
  cantidad,
  textoGarantia,
  textoRenovacion,
  tonoEstado,
  tonoGarantia,
  tonoRenovacion,
} from '../data/textos-activo';

/** Espera tras la última tecla antes de buscar, para no consultar la API letra por letra. */
const ESPERA_BUSQUEDA_MS = 350;

/**
 * Inventario de activos (docs/ARQUITECTURA.md §5.10): lista paginada en la API, con búsqueda y filtros que quedan
 * en la dirección. En pantallas angostas las filas se muestran como tarjetas.
 */
@Component({
  selector: 'app-inventario',
  imports: [
    LowerCasePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    AvisoComponent,
    SiTienePermisoDirective,
    SelectorCatalogoComponent,
  ],
  providers: [InventarioStore, { provide: MatPaginatorIntl, useClass: PaginadorEnEspanol }],
  templateUrl: './inventario.page.html',
  styleUrl: './inventario.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventarioPage {
  protected readonly store = inject(InventarioStore);
  private readonly api = inject(Api);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly permisos = Permisos;
  protected readonly tamanosPagina = TAMANOS_PAGINA;
  protected readonly textoGarantia = textoGarantia;
  protected readonly tonoGarantia = tonoGarantia;
  protected readonly textoRenovacion = textoRenovacion;
  protected readonly tonoRenovacion = tonoRenovacion;
  protected readonly tonoEstado = tonoEstado;
  protected readonly garantias: readonly NonNullable<EstadoGarantia>[] = ['Vigente', 'Vencida', 'SinDato'];
  protected readonly renovaciones: readonly NonNullable<SituacionRenovacion>[] = ['Vigente', 'ProximoARenovar', 'Renovar', 'SinDato'];

  /** Filtros de la dirección: son la única fuente; cambiar un filtro es cambiar la dirección. */
  protected readonly filtros = toSignal(
    this.ruta.queryParamMap.pipe(
      map((p) => filtrosDesdeParametros(Object.fromEntries(p.keys.map((k) => [k, p.get(k) ?? undefined])))),
    ),
    { requireSync: true },
  );

  protected readonly busqueda = new FormControl(this.filtros().buscar, { nonNullable: true });

  /** Establecimiento y tipo se eligen escribiendo (listas largas, D45). */
  protected readonly filtroEstablecimiento = new FormControl<string | null>(this.filtros().establecimientoId);
  protected readonly filtroTipo = new FormControl<string | null>(this.filtros().tipoActivoId);

  /** En pantallas angostas los filtros (salvo la búsqueda) se abren con un botón. */
  protected readonly filtrosAbiertos = signal(false);

  private readonly esSuperAdmin = this.sesion.esSuperAdmin;

  /** Sede consultada: la elegida en el encabezado (superadministrador) o nula (la API usa la del usuario). */
  private readonly sedeId = computed(() => (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));

  /** Con "Todas las sedes" se muestra la red de cada activo y no se filtra por establecimiento (son cientos). */
  protected readonly todasLasSedes = computed(() => this.esSuperAdmin() && this.sedeId() === null);

  protected readonly ambito = computed(() =>
    this.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? 'Todas las sedes') : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );

  protected readonly establecimientos = resource({
    params: () => (this.todasLasSedes() ? undefined : { sedeId: this.sedeId() ?? undefined }),
    loader: ({ params }) => this.api.invoke(listarEstablecimientos, { ...params, incluirInactivos: true }),
  });

  protected readonly tipos = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'tipos-activo', incluirInactivos: true }),
  });

  protected readonly estados = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'estados-activo', incluirInactivos: true }),
  });

  protected readonly conFiltros = computed(() => hayFiltros(this.filtros()));

  /** Filtros aplicados fuera de la búsqueda (para el botón "Filtros" en pantallas angostas). */
  protected readonly cantidadFiltros = computed(() => {
    const f = this.filtros();
    return [f.establecimientoId, f.tipoActivoId, f.estadoActivoId, f.garantia, f.renovacion].filter((x) => x !== null).length;
  });

  protected readonly subtitulo = computed(() => {
    const total = this.store.total();
    const activos = cantidad(total, 'activo', 'activos');
    return this.ambito() ? `${activos} · ${this.ambito()}` : activos;
  });

  constructor() {
    let sedeAnterior: string | null | undefined;

    effect(() => {
      const filtros = this.filtros();
      const sedeId = this.sedeId();

      untracked(() => {
        // Al cambiar de sede, el establecimiento elegido es de otra red: se quita y se vuelve a la primera página.
        if (sedeAnterior !== undefined && sedeAnterior !== sedeId && (filtros.establecimientoId || filtros.pagina !== 1)) {
          sedeAnterior = sedeId;
          this.aplicar({ ...filtros, establecimientoId: null, pagina: 1 });
          return;
        }
        sedeAnterior = sedeId;
        void this.store.cargar(filtros, sedeId);

        if (this.busqueda.value.trim() !== filtros.buscar) {
          this.busqueda.setValue(filtros.buscar, { emitEvent: false });
        }
        if (this.filtroEstablecimiento.value !== filtros.establecimientoId) {
          this.filtroEstablecimiento.setValue(filtros.establecimientoId, { emitEvent: false });
        }
        if (this.filtroTipo.value !== filtros.tipoActivoId) {
          this.filtroTipo.setValue(filtros.tipoActivoId, { emitEvent: false });
        }
      });
    });

    let espera: ReturnType<typeof setTimeout> | undefined;
    const suscripcion = this.busqueda.valueChanges.subscribe((texto) => {
      clearTimeout(espera);
      espera = setTimeout(() => {
        if (texto.trim() !== this.filtros().buscar) {
          this.aplicar(cambiarFiltro(this.filtros(), 'buscar', texto));
        }
      }, ESPERA_BUSQUEDA_MS);
    });
    const porEstablecimiento = this.filtroEstablecimiento.valueChanges.subscribe((id) => this.filtrar('establecimientoId', id));
    const porTipo = this.filtroTipo.valueChanges.subscribe((id) => this.filtrar('tipoActivoId', id));
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(espera);
      suscripcion.unsubscribe();
      porEstablecimiento.unsubscribe();
      porTipo.unsubscribe();
    });
  }

  protected filtrar<K extends 'establecimientoId' | 'tipoActivoId' | 'estadoActivoId' | 'garantia' | 'renovacion'>(
    campo: K,
    valor: FiltrosInventario[K],
  ): void {
    this.aplicar(cambiarFiltro(this.filtros(), campo, valor));
  }

  protected limpiarBusqueda(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar(cambiarFiltro(this.filtros(), 'buscar', ''));
  }

  protected quitarFiltros(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar(sinFiltros(this.filtros()));
  }

  protected cambiarPagina(evento: PageEvent): void {
    this.aplicar({ ...this.filtros(), pagina: evento.pageIndex + 1, tamanoPagina: evento.pageSize });
  }

  /** "Dell Latitude 5440". */
  protected equipo(a: ActivoResumenDto): string {
    return `${a.marca} ${a.modelo}`;
  }

  /** Estado de la dirección actual, para que la ficha pueda volver a esta misma lista. */
  protected volverAqui(): { volver: string } {
    return { volver: this.router.url };
  }

  private aplicar(filtros: FiltrosInventario): void {
    void this.router.navigate([], { relativeTo: this.ruta, queryParams: parametrosDesdeFiltros(filtros), replaceUrl: true });
  }
}
