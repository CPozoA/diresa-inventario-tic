import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { eliminarActivo } from '../../../api/fn/activos/eliminar-activo';
import { obtenerFichaActivo } from '../../../api/fn/activos/obtener-ficha-activo';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { aErrorApi } from '../../../core/http/error-api';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { rutaInternaSegura } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { cicloDeVida, datosTecnicos, detalleEstablecimiento } from '../data/presentacion-ficha';
import {
  aNumero,
  cantidad,
  formatoFecha,
  formatoGb,
  formatoSoles,
  textoAdquisicion,
  textoConservacion,
  textoCriticidad,
  textoGarantia,
  textoRenovacion,
  textoTipoDisco,
  textoTipoRam,
  textoUsoDisco,
  tonoEstado,
  tonoGarantia,
  tonoRenovacion,
} from '../data/textos-activo';

const LISTA = '/inventario';

/**
 * Ficha técnica de un activo, solo lectura (docs/ARQUITECTURA.md §5.10). Las secciones técnicas dependen de la ficha
 * del tipo (D43): Cómputo muestra todo, con discos y RAM; Red, solo nombre de equipo, IP y MAC; Ninguna, nada técnico.
 */
@Component({
  selector: 'app-ficha-activo',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, AvisoComponent, SiTienePermisoDirective],
  templateUrl: './ficha-activo.page.html',
  styleUrl: './ficha-activo.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FichaActivoPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly ubicacion = inject(Location);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  /** Viene de la ruta (:id). */
  readonly id = input.required<string>();

  protected readonly permisos = Permisos;
  protected readonly textoConservacion = textoConservacion;
  protected readonly textoCriticidad = textoCriticidad;
  protected readonly textoAdquisicion = textoAdquisicion;
  protected readonly textoGarantia = textoGarantia;
  protected readonly tonoGarantia = tonoGarantia;
  protected readonly textoRenovacion = textoRenovacion;
  protected readonly tonoRenovacion = tonoRenovacion;
  protected readonly textoTipoDisco = textoTipoDisco;
  protected readonly textoUsoDisco = textoUsoDisco;
  protected readonly textoTipoRam = textoTipoRam;
  protected readonly tonoEstado = tonoEstado;
  protected readonly formatoFecha = formatoFecha;
  protected readonly formatoGb = formatoGb;
  protected readonly formatoSoles = formatoSoles;
  protected readonly cantidad = cantidad;
  protected readonly aNumero = aNumero;

  protected readonly eliminando = signal(false);

  protected readonly recurso = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(obtenerFichaActivo, params),
  });

  protected readonly ficha = computed<FichaActivoDto | null>(() => (this.recurso.hasValue() ? this.recurso.value() : null));

  protected readonly error = computed(() => {
    const error = this.recurso.error();
    if (!error) {
      return null;
    }
    const e = aErrorApi(error);
    return e.estado === 404 ? 'Este activo no existe o pertenece a otra red.' : e.mensaje;
  });

  protected readonly tecnicos = computed(() => {
    const f = this.ficha();
    return f ? datosTecnicos(f) : [];
  });

  protected readonly ciclo = computed(() => {
    const f = this.ficha();
    return f ? cicloDeVida(f) : null;
  });

  protected readonly establecimiento = computed(() => {
    const f = this.ficha();
    return f ? detalleEstablecimiento(f) : '';
  });

  protected readonly tieneAdquisicion = computed(() => {
    const a = this.ficha()?.adquisicion;
    return !!a && (!!a.tipoAdquisicion || aNumero(a.valorAdquisicion) !== null || !!a.numeroOrdenCompra || !!a.proveedor);
  });

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

  protected async eliminar(ficha: FichaActivoDto): Promise<void> {
    const codigo = ficha.generales.codigoPatrimonial;
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Eliminar «${codigo}»`,
      mensaje:
        'Se eliminará la ficha con sus discos y su memoria RAM. Úselo solo para un registro hecho por error; un equipo que ya no se usa se marca con el estado «De baja».',
      confirmar: 'Eliminar',
      peligro: true,
    });
    if (!confirmado) {
      return;
    }

    this.eliminando.set(true);
    try {
      await this.api.invoke(eliminarActivo, { id: ficha.id });
      this.notificaciones.exito(`Se eliminó «${codigo}».`);
      await this.router.navigateByUrl(this.urlDeLista());
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.eliminando.set(false);
    }
  }
}
