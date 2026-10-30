import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, resource, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { Api } from '../../../api/api';
import { listarLicencias } from '../../../api/fn/licencias/listar-licencias';
import { obtenerCifrasLicencias } from '../../../api/fn/licencias/obtener-cifras-licencias';
import { FiltroLicencias } from '../../../api/models/filtro-licencias';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { valorDe } from '../../../shared/util/recurso';
import { aNumero, formatoFecha } from '../../activos/data/textos-activo';
import { abrirLicenciaDialog } from '../components/licencia.dialog';
import {
  FILTROS_LICENCIAS_INICIALES,
  FiltrosLicencias,
  OPCIONES_FILTRO,
  filtrosLicenciasDesdeParametros,
  parametrosDesdeFiltrosLicencias,
  porcentajeUso,
  textoLibres,
  textoPuestos,
  textoVigencia,
  tonoVigencia,
} from '../data/licencias';

const ESPERA_BUSQUEDA_MS = 350;

/**
 * Licencias de software de la red (D54): cifras (vigentes, por vencer, vencidas, con equipos de más) y la lista, la que
 * vence antes primero. Búsqueda y filtro quedan en la dirección.
 */
@Component({
  selector: 'app-licencias',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    AvisoComponent,
  ],
  templateUrl: './licencias.page.html',
  styleUrl: './licencias.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenciasPage {
  private readonly api = inject(Api);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly opciones = OPCIONES_FILTRO;
  protected readonly formatoFecha = formatoFecha;
  protected readonly textoVigencia = textoVigencia;
  protected readonly tonoVigencia = tonoVigencia;
  protected readonly textoPuestos = textoPuestos;
  protected readonly textoLibres = textoLibres;
  protected readonly porcentajeUso = porcentajeUso;
  protected readonly puedeGestionar = this.sesion.tienePermiso(Permisos.Licencias.Gestionar);

  protected readonly filtros = toSignal(
    this.ruta.queryParamMap.pipe(
      map((p) => filtrosLicenciasDesdeParametros(Object.fromEntries(p.keys.map((k) => [k, p.get(k) ?? undefined])))),
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

  protected readonly cifras = resource({
    params: () => ({ sedeId: this.sedeId() }),
    loader: ({ params }) => this.api.invoke(obtenerCifrasLicencias, params.sedeId ? { sedeId: params.sedeId } : {}),
  });

  protected readonly lista = resource({
    params: () => {
      const f = this.filtros();
      const sedeId = this.sedeId();
      return { ...(sedeId ? { sedeId } : {}), ...(f.buscar ? { buscar: f.buscar } : {}), ...(f.filtro ? { filtro: f.filtro } : {}) };
    },
    loader: ({ params }) => this.api.invoke(listarLicencias, params),
  });

  protected readonly filas = computed(() => valorDe(this.lista) ?? []);
  protected readonly error = computed(() => (this.lista.error() ? aErrorApi(this.lista.error()).mensaje : null));
  protected readonly conFiltros = computed(() => this.filtros().buscar !== '' || this.filtros().filtro !== null);

  protected readonly vigentes = computed(() => aNumero(valorDe(this.cifras)?.vigentes) ?? 0);
  protected readonly porVencer = computed(() => aNumero(valorDe(this.cifras)?.porVencer) ?? 0);
  protected readonly vencidas = computed(() => aNumero(valorDe(this.cifras)?.vencidas) ?? 0);
  protected readonly excedidas = computed(() => aNumero(valorDe(this.cifras)?.excedidas) ?? 0);
  protected readonly libres = computed(() => aNumero(valorDe(this.cifras)?.puestosLibres) ?? 0);
  protected readonly dias = computed(() => aNumero(valorDe(this.cifras)?.dias) ?? 30);

  constructor() {
    effect(() => {
      const buscar = this.filtros().buscar;
      untracked(() => {
        if (this.busqueda.value.trim() !== buscar) {
          this.busqueda.setValue(buscar, { emitEvent: false });
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
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(espera);
      porTexto.unsubscribe();
    });
  }

  protected filtrar(filtro: NonNullable<FiltroLicencias> | null): void {
    this.aplicar({ ...this.filtros(), filtro: this.filtros().filtro === filtro ? null : filtro });
  }

  protected elegirFiltro(filtro: NonNullable<FiltroLicencias> | null): void {
    this.aplicar({ ...this.filtros(), filtro });
  }

  protected limpiarBusqueda(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar({ ...this.filtros(), buscar: '' });
  }

  protected quitarFiltros(): void {
    this.busqueda.setValue('', { emitEvent: false });
    this.aplicar(FILTROS_LICENCIAS_INICIALES);
  }

  protected async nueva(): Promise<void> {
    const id = await abrirLicenciaDialog(this.dialogos, { ficha: null });
    if (id) {
      this.notificaciones.exito('Licencia registrada. Ahora asígnele los equipos.');
      void this.router.navigate(['/licencias', id]);
    }
  }

  protected volverAqui(): { volver: string } {
    return { volver: this.router.url };
  }

  private aplicar(filtros: FiltrosLicencias): void {
    void this.router.navigate([], {
      relativeTo: this.ruta,
      queryParams: parametrosDesdeFiltrosLicencias(filtros),
      replaceUrl: true,
    });
  }
}
