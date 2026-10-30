import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { Api } from '../../api/api';
import { obtenerResumen } from '../../api/fn/resumen/obtener-resumen';
import { ResumenDto } from '../../api/models/resumen-dto';
import { Permisos } from '../../core/auth/permisos';
import { SesionStore } from '../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../core/auth/si-tiene-permiso.directive';
import { aErrorApi } from '../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../shared/ui/aviso.component';
import { aNumero, textoAtencion, tonoAtencion } from '../activos/data/textos-activo';
import { AlertasLicenciasComponent } from '../licencias/components/alertas-licencias.component';
import { abrirEstablecimientosSinActivos } from './establecimientos-sin-activos.dialog';
import {
  FilaRed,
  OrdenRedes,
  anchoRelativo,
  barrasAntiguedad,
  colorGrupo,
  detalleAtencion,
  fechaCorta,
  filaRed,
  miles,
  ordenarRedes,
  porcentaje,
  tarjetas,
  totalRedes,
} from './presentacion-resumen';

/** Cuántos tipos se dibujan como barra; el resto se suma en "Otros tipos". */
const MAXIMO_TIPOS = 8;

/**
 * Resumen del inicio (docs/ARQUITECTURA.md §5.11). Un usuario de red ve la suya; el superadministrador ve todas
 * (con la tabla por red) o la elegida en el encabezado. Casi todo es un enlace al inventario ya filtrado.
 */
@Component({
  selector: 'app-resumen',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, AvisoComponent, SiTienePermisoDirective, AlertasLicenciasComponent],
  templateUrl: './resumen.page.html',
  styleUrl: './resumen.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResumenPage {
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly permisos = Permisos;
  protected readonly miles = miles;
  protected readonly textoAtencion = textoAtencion;
  protected readonly tonoAtencion = tonoAtencion;

  /** Sin Activos.Ver no hay nada que resumir (por ejemplo, un usuario que solo gestiona usuarios). */
  protected readonly puedeVer = computed(() => this.sesion.tienePermiso(Permisos.Activos.Ver));
  private readonly esSuperAdmin = this.sesion.esSuperAdmin;

  /** Avisos de licencias (D54) en "Requieren atención", con Licencias.Ver. */
  protected readonly verLicencias = this.sesion.tienePermiso(Permisos.Licencias.Ver);
  protected readonly sedeLicencias = computed(() => (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));

  protected readonly recurso = resource({
    params: () =>
      this.puedeVer() ? { sedeId: (this.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null) ?? undefined } : undefined,
    loader: ({ params }) => this.api.invoke(obtenerResumen, params),
  });

  /** El último resumen recibido: se conserva mientras se carga otro, para que la pantalla no parpadee. */
  protected readonly resumen = computed<ResumenDto | null>(() => (this.recurso.hasValue() ? this.recurso.value() : null));

  protected readonly error = computed(() => {
    const error = this.recurso.error();
    return error ? aErrorApi(error).mensaje : null;
  });

  protected readonly ambito = computed(() => {
    const r = this.resumen();
    if (r?.sedeNombre) {
      return r.sedeNombre;
    }
    return this.esSuperAdmin() ? 'Las redes de salud' : (this.sesion.sesion()?.sede?.nombre ?? '');
  });

  protected readonly fecha = computed(() => {
    const r = this.resumen();
    return r ? fechaCorta(r.fecha) : '';
  });

  /** Vista del superadministrador con todas las redes. */
  protected readonly todas = computed(() => (this.resumen()?.redes ?? null) !== null);

  protected readonly vacio = computed(() => {
    const r = this.resumen();
    return r !== null && n(r.cifras.activos) === 0;
  });

  protected readonly tarjetas = computed(() => {
    const r = this.resumen();
    return r ? tarjetas(r.cifras) : [];
  });

  protected readonly total = computed(() => n(this.resumen()?.cifras.activos));

  protected readonly estados = computed(() => {
    const r = this.resumen();
    if (!r) {
      return [];
    }
    const total = n(r.cifras.activos);
    return r.estados.map((e) => ({
      id: e.estadoActivoId,
      nombre: e.nombre,
      cantidad: n(e.cantidad),
      porcentaje: porcentaje(n(e.cantidad), total),
      ancho: total > 0 ? (n(e.cantidad) / total) * 100 : 0,
      color: colorGrupo(e.grupo),
    }));
  });

  protected readonly estadosAria = computed(() =>
    this.estados()
      .map((e) => `${e.nombre}: ${e.cantidad}`)
      .join(', '),
  );

  protected readonly antiguedad = computed(() => barrasAntiguedad(this.resumen()?.antiguedad ?? []));
  protected readonly sinFecha = computed(() => this.antiguedad().find((a) => a.atenuado)?.cantidad ?? 0);

  protected readonly tipos = computed(() => {
    const tipos = this.resumen()?.tipos ?? [];
    const maximo = Math.max(1, ...tipos.map((t) => n(t.cantidad)));
    return tipos.slice(0, MAXIMO_TIPOS).map((t) => ({
      id: t.tipoActivoId,
      nombre: t.nombre,
      cantidad: n(t.cantidad),
      ancho: anchoRelativo(n(t.cantidad), maximo),
    }));
  });

  /** Activos de los tipos que no entran en las barras. */
  protected readonly otrosTipos = computed(() =>
    (this.resumen()?.tipos ?? []).slice(MAXIMO_TIPOS).reduce((total, t) => total + n(t.cantidad), 0),
  );

  protected readonly establecimientos = computed(() => {
    const e = this.resumen()?.establecimientos;
    if (!e) {
      return null;
    }
    const total = n(e.total);
    const sinActivos = n(e.sinActivos);
    return {
      total,
      sinActivos,
      conActivos: total - sinActivos,
      principales: e.principales.map((p) => ({
        id: p.establecimientoId,
        nombre: p.nombre,
        activos: n(p.activos),
        enUso: n(p.enUso),
        renovar: n(p.renovar),
      })),
    };
  });

  protected readonly atencion = computed(() => {
    const r = this.resumen();
    return r ? r.atencion.primeros.map((a) => ({ ...a, detalle: detalleAtencion(a, r.fecha) })) : [];
  });

  protected readonly totalAtencion = computed(() => n(this.resumen()?.atencion.total));

  protected readonly fichas = computed(() => {
    const r = this.resumen();
    if (!r) {
      return [];
    }
    const evaluadas = n(r.fichas.evaluadas);
    return [
      { texto: 'Sin fotos', cantidad: n(r.fichas.sinFotos), falta: 'Fotos' },
      { texto: 'Sin fecha de adquisición', cantidad: n(r.fichas.sinFechaAdquisicion), falta: 'FechaAdquisicion' },
      { texto: 'Sin responsable', cantidad: n(r.fichas.sinResponsable), falta: 'Responsable' },
      { texto: 'Sin número de serie', cantidad: n(r.fichas.sinNumeroSerie), falta: 'NumeroSerie' },
    ].map((f) => ({ ...f, ancho: anchoRelativo(f.cantidad, evaluadas) }));
  });

  protected readonly fichasCompletas = computed(() => {
    const r = this.resumen();
    return r ? porcentaje(n(r.fichas.completas), n(r.fichas.evaluadas)) : '—';
  });

  // ---------- Tabla por red (superadministrador) ----------

  protected readonly orden = signal<OrdenRedes>('activos');
  private readonly filasRedes = computed(() => (this.resumen()?.redes ?? []).map(filaRed));
  protected readonly redes = computed(() => ordenarRedes(this.filasRedes(), this.orden()));
  protected readonly totalRedes = computed<FilaRed | null>(() => {
    const redes = this.resumen()?.redes;
    return redes ? totalRedes(this.filasRedes(), redes) : null;
  });

  protected ordenar(evento: Event): void {
    this.orden.set((evento.target as HTMLSelectElement).value as OrdenRedes);
  }

  /** Abre el resumen de una red: es la misma elección que el selector del encabezado. */
  protected verRed(fila: FilaRed): void {
    this.sedeSeleccionada.seleccionar(fila.sedeId);
  }

  protected verSinActivos(): void {
    const r = this.resumen();
    if (r) {
      abrirEstablecimientosSinActivos(this.dialogos, { sedeId: r.sedeId ?? null, red: r.sedeNombre ?? '' });
    }
  }
}

function n(valor: number | string | null | undefined): number {
  return aNumero(valor) ?? 0;
}
