import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { archivarLicencia } from '../../../api/fn/licencias/archivar-licencia';
import { eliminarLicencia } from '../../../api/fn/licencias/eliminar-licencia';
import { obtenerLicencia } from '../../../api/fn/licencias/obtener-licencia';
import { quitarLicencia } from '../../../api/fn/licencias/quitar-licencia';
import { reactivarLicencia } from '../../../api/fn/licencias/reactivar-licencia';
import { EquipoLicenciaDto } from '../../../api/models/equipo-licencia-dto';
import { FichaLicenciaDto } from '../../../api/models/ficha-licencia-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { aErrorApi } from '../../../core/http/error-api';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { rutaInternaSegura } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { valorDe } from '../../../shared/util/recurso';
import { aNumero, formatoFecha, formatoSoles } from '../../activos/data/textos-activo';
import { abrirAsignarEquipos } from '../components/asignar-equipos.dialog';
import { abrirLicenciaDialog } from '../components/licencia.dialog';
import { porcentajeUso, textoLibres, textoPuestos, textoVigencia, tonoVigencia } from '../data/licencias';

const LISTA = '/licencias';

/**
 * Ficha de una licencia (D54): sus datos, los puestos ocupados y los equipos que la tienen. Con Licencias.Gestionar se
 * asignan y quitan equipos, se edita, se renueva (nuevas fechas), se archiva (no se renovó) o se elimina.
 */
@Component({
  selector: 'app-ficha-licencia',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule, AvisoComponent],
  templateUrl: './ficha-licencia.page.html',
  styleUrl: './ficha-licencia.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FichaLicenciaPage {
  /** Identificador de la ruta (licencias/:id). */
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly router = inject(Router);
  private readonly ubicacion = inject(Location);
  private readonly sesion = inject(SesionStore);

  protected readonly formatoFecha = formatoFecha;
  protected readonly textoVigencia = textoVigencia;
  protected readonly tonoVigencia = tonoVigencia;
  protected readonly textoPuestos = textoPuestos;
  protected readonly textoLibres = textoLibres;
  protected readonly porcentajeUso = porcentajeUso;
  protected readonly esSuperAdmin = this.sesion.esSuperAdmin;
  protected readonly puedeGestionar = this.sesion.tienePermiso(Permisos.Licencias.Gestionar);

  protected readonly recurso = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(obtenerLicencia, params),
  });

  protected readonly licencia = computed(() => valorDe(this.recurso) ?? null);
  protected readonly error = computed(() => (this.recurso.error() ? aErrorApi(this.recurso.error()).mensaje : null));

  protected readonly puestos = computed(() => aNumero(this.licencia()?.puestos) ?? 0);
  protected readonly equipos = computed(() => this.licencia()?.equipos ?? []);
  protected readonly deMas = computed(() => Math.max(0, this.equipos().length - this.puestos()));
  protected readonly libres = computed(() => Math.max(0, this.puestos() - this.equipos().length));
  protected readonly deBaja = computed(() => this.equipos().filter((e) => e.grupoEstado === 'DeBaja').length);
  protected readonly archivada = computed(() => this.licencia()?.vigencia === 'Archivada');
  protected readonly vencida = computed(() => this.licencia()?.vigencia === 'Vencida');
  /** Renovar se destaca cuando está por vencer o vencida. */
  protected readonly renovarPrimero = computed(() => ['PorVencer', 'Vencida'].includes(this.licencia()?.vigencia ?? ''));

  protected readonly trabajando = signal(false);

  protected costo(l: FichaLicenciaDto): string {
    return l.costo === null || l.costo === undefined || l.costo === '' ? '—' : formatoSoles(l.costo);
  }

  protected equipo(e: EquipoLicenciaDto): string {
    return `${e.marca} ${e.modelo} · ${e.tipoActivo}`;
  }

  protected async asignar(l: FichaLicenciaDto): Promise<void> {
    if (await abrirAsignarEquipos(this.dialogos, { licencia: l })) {
      this.notificaciones.exito('Equipos asignados.');
      this.recurso.reload();
    }
  }

  protected async editar(l: FichaLicenciaDto, renovar = false): Promise<void> {
    if (await abrirLicenciaDialog(this.dialogos, { ficha: l, renovar })) {
      this.notificaciones.exito(renovar ? 'Licencia renovada.' : 'Datos guardados.');
      this.recurso.reload();
    }
  }

  protected async quitar(l: FichaLicenciaDto, e: EquipoLicenciaDto): Promise<void> {
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Quitar ${e.codigoPatrimonial}`,
      mensaje: `El equipo deja de tener ${l.producto} y se libera su puesto.`,
      confirmar: 'Quitar',
    });
    if (confirmado) {
      await this.ejecutar(() => this.api.invoke(quitarLicencia, { id: l.id, activoId: e.activoId }), 'Equipo quitado.');
    }
  }

  protected async archivar(l: FichaLicenciaDto): Promise<void> {
    const n = l.equipos.length;
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Archivar ${l.producto}`,
      mensaje:
        'Úselo cuando la suscripción no se renovará: deja de contar en los avisos' +
        (n > 0 ? ` y se quita de ${n === 1 ? 'su equipo' : `sus ${n} equipos`}.` : '.') +
        ' Se puede reactivar después.',
      confirmar: 'Archivar',
    });
    if (confirmado) {
      await this.ejecutar(() => this.api.invoke(archivarLicencia, { id: l.id }), 'Licencia archivada.');
    }
  }

  protected async reactivar(l: FichaLicenciaDto): Promise<void> {
    await this.ejecutar(() => this.api.invoke(reactivarLicencia, { id: l.id }), 'Licencia reactivada.');
  }

  protected async eliminar(l: FichaLicenciaDto): Promise<void> {
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Eliminar ${l.producto}`,
      mensaje: 'Solo para una licencia registrada por error: se borra con sus equipos y no se puede deshacer. Si no se renovó, mejor archívela.',
      confirmar: 'Eliminar',
      peligro: true,
    });
    if (!confirmado) {
      return;
    }
    this.trabajando.set(true);
    try {
      await this.api.invoke(eliminarLicencia, { id: l.id });
      this.notificaciones.exito('Licencia eliminada.');
      void this.router.navigateByUrl(this.urlDeLista());
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.trabajando.set(false);
    }
  }

  /** Vuelve a la lista con los mismos filtros con que se abrió la ficha (si se llegó desde ella). */
  protected volver(evento: Event): void {
    evento.preventDefault();
    void this.router.navigateByUrl(this.urlDeLista());
  }

  protected urlDeLista(): string {
    const estado = this.ubicacion.getState() as { volver?: unknown } | null;
    const volver = typeof estado?.volver === 'string' ? rutaInternaSegura(estado.volver) : LISTA;
    return volver === LISTA || volver.startsWith(`${LISTA}?`) ? volver : LISTA;
  }

  private async ejecutar(accion: () => Promise<unknown>, exito: string): Promise<void> {
    if (this.trabajando()) {
      return;
    }
    this.trabajando.set(true);
    try {
      await accion();
      this.notificaciones.exito(exito);
      this.recurso.reload();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.trabajando.set(false);
    }
  }
}
