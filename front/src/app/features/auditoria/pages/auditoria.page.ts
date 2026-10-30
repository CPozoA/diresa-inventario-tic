import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, resource, signal, untracked } from '@angular/core';
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
import { exportarAuditoria } from '../../../api/fn/auditoria/exportar-auditoria';
import { listarAuditoria } from '../../../api/fn/auditoria/listar-auditoria';
import { ModuloAuditoria } from '../../../api/models/modulo-auditoria';
import { SesionStore } from '../../../core/auth/sesion.store';
import { guardarArchivo, nombreDesdeEncabezado } from '../../../core/http/descarga';
import { aErrorApi, aErrorApiDeDescarga } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { PaginadorEnEspanol } from '../../../shared/ui/paginador';
import { valorDe } from '../../../shared/util/recurso';
import { aNumero } from '../../activos/data/textos-activo';
import {
  FILTROS_AUDITORIA_INICIALES,
  FiltrosAuditoria,
  MODULOS,
  esAlerta,
  fechaHora,
  filtrosAuditoriaDesdeParametros,
  hayFiltrosAuditoria,
  parametrosDesdeFiltrosAuditoria,
  textoModulo,
} from '../data/auditoria';

const ESPERA_BUSQUEDA_MS = 350;
const TAMANO_PAGINA = 50;

/**
 * Registro de auditoría (D56): quién hizo qué y cuándo, lo más reciente primero. Un administrador de sede ve lo de su
 * red; el superadministrador, todo o la red elegida arriba. Filtros en la dirección y exportación a Excel.
 */
@Component({
  selector: 'app-auditoria',
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
    AvisoComponent,
  ],
  providers: [{ provide: MatPaginatorIntl, useClass: PaginadorEnEspanol }],
  templateUrl: './auditoria.page.html',
  styleUrl: './auditoria.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditoriaPage {
  private readonly api = inject(Api);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly modulos = MODULOS;
  protected readonly textoModulo = textoModulo;
  protected readonly esAlerta = esAlerta;
  protected readonly fechaHora = fechaHora;
  protected readonly tamanoPagina = TAMANO_PAGINA;

  protected readonly filtros = toSignal(
    this.ruta.queryParamMap.pipe(
      map((p) => filtrosAuditoriaDesdeParametros(Object.fromEntries(p.keys.map((k) => [k, p.get(k) ?? undefined])))),
    ),
    { requireSync: true },
  );

  protected readonly busqueda = new FormControl(this.filtros().buscar, { nonNullable: true });

  private readonly esSuperAdmin = this.sesion.esSuperAdmin;
  private readonly sedeId = computed(() => (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));
  protected readonly todasLasSedes = computed(() => this.esSuperAdmin() && this.sedeId() === null);
  protected readonly ambito = computed(() =>
    this.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? 'todas las redes') : (this.sesion.sesion()?.sede?.nombre ?? 'la red'),
  );

  private readonly consulta = computed(() => {
    const f = this.filtros();
    const sedeId = this.sedeId();
    return {
      ...(sedeId ? { sedeId } : {}),
      ...(f.desde ? { desde: f.desde } : {}),
      ...(f.hasta ? { hasta: f.hasta } : {}),
      ...(f.modulo ? { modulo: f.modulo } : {}),
      ...(f.buscar ? { buscar: f.buscar } : {}),
    };
  });

  protected readonly lista = resource({
    params: () => ({ ...this.consulta(), pagina: this.filtros().pagina, tamanoPagina: TAMANO_PAGINA }),
    loader: ({ params }) => this.api.invoke(listarAuditoria, params),
  });

  protected readonly registros = computed(() => valorDe(this.lista)?.registros ?? []);
  protected readonly total = computed(() => aNumero(valorDe(this.lista)?.total) ?? 0);
  protected readonly error = computed(() => (this.lista.error() ? aErrorApi(this.lista.error()).mensaje : null));
  protected readonly conFiltros = computed(() => hayFiltrosAuditoria(this.filtros()));
  protected readonly exportando = signal(false);

  constructor() {
    let sedeAnterior: string | null | undefined;
    effect(() => {
      const filtros = this.filtros();
      const sedeId = this.sedeId();
      untracked(() => {
        if (sedeAnterior !== undefined && sedeAnterior !== sedeId && filtros.pagina > 1) {
          sedeAnterior = sedeId;
          this.aplicar({ ...filtros, pagina: 1 });
          return;
        }
        sedeAnterior = sedeId;
        if (this.busqueda.value.trim() !== filtros.buscar) {
          this.busqueda.setValue(filtros.buscar, { emitEvent: false });
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
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(espera);
      porTexto.unsubscribe();
    });
  }

  protected cambiarFecha(campo: 'desde' | 'hasta', valor: string): void {
    this.aplicar({ ...this.filtros(), [campo]: valor, pagina: 1 });
  }

  protected filtrarModulo(modulo: NonNullable<ModuloAuditoria> | null): void {
    this.aplicar({ ...this.filtros(), modulo, pagina: 1 });
  }

  protected limpiarBusqueda(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...this.filtros(), buscar: '', pagina: 1 });
  }

  protected quitarFiltros(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar(FILTROS_AUDITORIA_INICIALES);
  }

  protected cambiarPagina(evento: PageEvent): void {
    this.aplicar({ ...this.filtros(), pagina: evento.pageIndex + 1 });
  }

  protected async exportar(): Promise<void> {
    if (this.exportando()) {
      return;
    }
    this.exportando.set(true);
    try {
      const respuesta = await this.api.invoke$Response(exportarAuditoria, this.consulta());
      const nombre = nombreDesdeEncabezado(respuesta.headers.get('Content-Disposition'), 'Auditoria.xlsx');
      guardarArchivo(respuesta.body, nombre);
      this.notificaciones.exito(`Se descargó ${nombre}.`);
    } catch (error: unknown) {
      this.notificaciones.aviso((await aErrorApiDeDescarga(error)).mensaje);
    } finally {
      this.exportando.set(false);
    }
  }

  private aplicar(filtros: FiltrosAuditoria): void {
    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: parametrosDesdeFiltrosAuditoria(filtros),
      replaceUrl: true,
    });
  }
}
