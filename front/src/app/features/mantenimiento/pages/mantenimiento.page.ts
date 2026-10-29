import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, resource, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { Api } from '../../../api/api';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { listarMantenimientos } from '../../../api/fn/mantenimiento/listar-mantenimientos';
import { listarMantenimientosProgramados } from '../../../api/fn/mantenimiento/listar-mantenimientos-programados';
import { obtenerCifrasMantenimiento } from '../../../api/fn/mantenimiento/obtener-cifras-mantenimiento';
import { MantenimientoDto } from '../../../api/models/mantenimiento-dto';
import { ProgramadoDto } from '../../../api/models/programado-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { PaginadorEnEspanol } from '../../../shared/ui/paginador';
import { hoyIso } from '../../activos/data/formulario-activo';
import { aNumero, formatoFecha } from '../../activos/data/textos-activo';
import { SelectorCatalogoComponent } from '../../catalogos/components/selector-catalogo.component';
import { abrirCerrarMantenimiento } from '../components/cerrar-mantenimiento.dialog';
import { abrirRegistrarMantenimiento } from '../components/registrar-mantenimiento.dialog';
import {
  FILTROS_MANTENIMIENTO_INICIALES,
  FiltrosMantenimiento,
  TIPOS_MANTENIMIENTO,
  VistaPagina,
  cuandoToca,
  fechasMantenimiento,
  filtrosMantenimientoDesdeParametros,
  haceCuanto,
  parametrosDesdeFiltrosMantenimiento,
  quien,
  textoCosto,
  textoResultado,
  textoTipo,
  tonoResultado,
  tonoTipo,
} from '../data/mantenimiento';
import { valorDe } from '../../../shared/util/recurso';

const ESPERA_BUSQUEDA_MS = 350;
const TAMANO_PAGINA = 25;

/**
 * Mantenimiento de la red (D53): cifras, y las pestañas En curso, Programados (preventivos vencidos y próximos) e
 * Historial. Pestaña y filtros quedan en la dirección.
 */
@Component({
  selector: 'app-mantenimiento',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTabsModule,
    AvisoComponent,
    SelectorCatalogoComponent,
  ],
  providers: [{ provide: MatPaginatorIntl, useClass: PaginadorEnEspanol }],
  templateUrl: './mantenimiento.page.html',
  styleUrl: './mantenimiento.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MantenimientoPage {
  protected readonly valorDe = valorDe;
  private readonly api = inject(Api);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly tipos = TIPOS_MANTENIMIENTO;
  protected readonly tamanoPagina = TAMANO_PAGINA;
  protected readonly hoy = hoyIso();
  protected readonly formatoFecha = formatoFecha;
  protected readonly textoTipo = textoTipo;
  protected readonly tonoTipo = tonoTipo;
  protected readonly textoResultado = textoResultado;
  protected readonly tonoResultado = tonoResultado;
  protected readonly fechas = fechasMantenimiento;
  protected readonly quien = quien;
  protected readonly textoCosto = textoCosto;
  protected readonly haceCuanto = haceCuanto;
  protected readonly puedeGestionar = this.sesion.tienePermiso(Permisos.Mantenimiento.Gestionar);

  protected readonly filtros = toSignal(
    this.ruta.queryParamMap.pipe(
      map((p) => filtrosMantenimientoDesdeParametros(Object.fromEntries(p.keys.map((k) => [k, p.get(k) ?? undefined])))),
    ),
    { requireSync: true },
  );

  protected readonly busqueda = new FormControl(this.filtros().buscar, { nonNullable: true });
  protected readonly filtroEstablecimiento = new FormControl<string | null>(this.filtros().establecimientoId);

  private readonly esSuperAdmin = this.sesion.esSuperAdmin;
  private readonly sedeId = computed(() => (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));
  protected readonly todasLasSedes = computed(() => this.esSuperAdmin() && this.sedeId() === null);
  protected readonly ambito = computed(() =>
    this.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? 'todas las redes') : (this.sesion.sesion()?.sede?.nombre ?? 'la red'),
  );

  protected readonly vista = computed(() => this.filtros().vista);

  protected readonly cifras = resource({
    params: () => ({ sedeId: this.sedeId() }),
    loader: ({ params }) => this.api.invoke(obtenerCifrasMantenimiento, params.sedeId ? { sedeId: params.sedeId } : {}),
  });

  protected readonly lista = resource({
    params: () => {
      const f = this.filtros();
      if (f.vista === 'programados') {
        return undefined;
      }
      const sedeId = this.sedeId();
      return {
        vista: f.vista === 'curso' ? ('EnCurso' as const) : ('Historial' as const),
        pagina: f.pagina,
        tamanoPagina: TAMANO_PAGINA,
        ...(sedeId ? { sedeId } : {}),
        ...(f.buscar ? { buscar: f.buscar } : {}),
        ...(f.tipo ? { tipo: f.tipo } : {}),
        ...(f.establecimientoId && !this.todasLasSedes() ? { establecimientoId: f.establecimientoId } : {}),
      };
    },
    loader: ({ params }) => this.api.invoke(listarMantenimientos, params),
  });

  protected readonly programados = resource({
    params: () => {
      const f = this.filtros();
      if (f.vista !== 'programados') {
        return undefined;
      }
      const sedeId = this.sedeId();
      return {
        ...(sedeId ? { sedeId } : {}),
        ...(f.buscar ? { buscar: f.buscar } : {}),
        ...(f.establecimientoId && !this.todasLasSedes() ? { establecimientoId: f.establecimientoId } : {}),
      };
    },
    loader: ({ params }) => this.api.invoke(listarMantenimientosProgramados, params),
  });

  protected readonly establecimientos = resource({
    params: () => (this.todasLasSedes() ? undefined : { sedeId: this.sedeId() ?? undefined }),
    loader: ({ params }) => this.api.invoke(listarEstablecimientos, { ...params, incluirInactivos: true }),
  });

  protected readonly filas = computed(() => (this.lista.hasValue() ? this.lista.value().mantenimientos : []));
  protected readonly total = computed(() => (this.lista.hasValue() ? (aNumero(this.lista.value().total) ?? 0) : 0));
  protected readonly filasProgramadas = computed(() => (this.programados.hasValue() ? this.programados.value() : []));
  protected readonly cargando = computed(() => this.lista.isLoading() || this.programados.isLoading());
  protected readonly error = computed(() => {
    const e = this.lista.error() ?? this.programados.error();
    return e ? aErrorApi(e).mensaje : null;
  });

  protected readonly enCurso = computed(() => aNumero(valorDe(this.cifras)?.enCurso) ?? 0);
  protected readonly vencidos = computed(() => aNumero(valorDe(this.cifras)?.vencidos) ?? 0);
  protected readonly proximos = computed(() => aNumero(valorDe(this.cifras)?.proximos) ?? 0);
  protected readonly dias = computed(() => aNumero(valorDe(this.cifras)?.dias) ?? 30);

  protected readonly conFiltros = computed(() => {
    const f = this.filtros();
    return f.buscar !== '' || f.tipo !== null || f.establecimientoId !== null;
  });

  constructor() {
    let sedeAnterior: string | null | undefined;
    effect(() => {
      const filtros = this.filtros();
      const sedeId = this.sedeId();
      untracked(() => {
        if (sedeAnterior !== undefined && sedeAnterior !== sedeId && (filtros.establecimientoId || filtros.pagina > 1)) {
          sedeAnterior = sedeId;
          this.aplicar({ ...filtros, establecimientoId: null, pagina: 1 });
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
          this.aplicar({ ...this.filtros(), buscar: texto, pagina: 1 });
        }
      }, ESPERA_BUSQUEDA_MS);
    });
    const porEstablecimiento = this.filtroEstablecimiento.valueChanges.subscribe((id) =>
      this.aplicar({ ...this.filtros(), establecimientoId: id, pagina: 1 }),
    );
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(espera);
      porTexto.unsubscribe();
      porEstablecimiento.unsubscribe();
    });
  }

  protected verVista(vista: VistaPagina): void {
    if (vista !== this.vista()) {
      this.aplicar({ ...this.filtros(), vista, pagina: 1, tipo: vista === 'programados' ? null : this.filtros().tipo });
    }
  }

  protected filtrarTipo(tipo: FiltrosMantenimiento['tipo']): void {
    this.aplicar({ ...this.filtros(), tipo, pagina: 1 });
  }

  protected limpiarBusqueda(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...this.filtros(), buscar: '', pagina: 1 });
  }

  protected quitarFiltros(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...FILTROS_MANTENIMIENTO_INICIALES, vista: this.vista() });
  }

  protected cambiarPagina(evento: PageEvent): void {
    this.aplicar({ ...this.filtros(), pagina: evento.pageIndex + 1 });
  }

  protected equipo(m: { marca: string; modelo: string; tipoActivo: string }): string {
    return `${m.marca} ${m.modelo} · ${m.tipoActivo}`;
  }

  protected cuando(p: ProgramadoDto): { texto: string; tono: string } {
    return cuandoToca(p.proximoMantenimiento, this.hoy);
  }

  protected async registrar(activoId?: string): Promise<void> {
    const registrado = await abrirRegistrarMantenimiento(this.dialogos, activoId ? { activoId, tipo: 'Preventivo' } : {});
    if (registrado) {
      this.notificaciones.exito('Mantenimiento registrado.');
      this.recargar();
    }
  }

  protected async cerrar(m: MantenimientoDto): Promise<void> {
    if (await abrirCerrarMantenimiento(this.dialogos, m)) {
      this.notificaciones.exito('Mantenimiento cerrado.');
      this.recargar();
    }
  }

  private recargar(): void {
    this.cifras.reload();
    this.lista.reload();
    this.programados.reload();
  }

  private aplicar(filtros: FiltrosMantenimiento): void {
    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: parametrosDesdeFiltrosMantenimiento(filtros),
      replaceUrl: true,
    });
  }
}
