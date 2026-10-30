import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { obtenerLicenciasActivo } from '../../../api/fn/licencias/obtener-licencias-activo';
import { quitarLicencia } from '../../../api/fn/licencias/quitar-licencia';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { LicenciaDelActivoDto } from '../../../api/models/licencia-del-activo-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { valorDe } from '../../../shared/util/recurso';
import { formatoFecha } from '../../activos/data/textos-activo';
import { textoVigencia, tonoVigencia } from '../data/licencias';
import { abrirAgregarLicencia } from './agregar-licencia.dialog';

/**
 * Licencias de software instaladas en el activo (D54), en su ficha. Con Licencias.Gestionar se agrega una licencia
 * vigente con puestos libres o se quita.
 */
@Component({
  selector: 'app-licencias-activo',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="tarjeta" aria-labelledby="titulo-licencias">
      <h2 id="titulo-licencias">
        Licencias
        @if (lista().length > 0) {
          <span class="atenuado cuenta">· {{ lista().length }}</span>
        }
      </h2>
      @if (lista().length > 0) {
        <ul class="lista">
          @for (l of lista(); track l.licenciaId) {
            <li>
              <span class="datos">
                <a class="producto" [routerLink]="['/licencias', l.licenciaId]">{{ l.producto }}</a>
                <span class="atenuado">
                  <span class="pastilla" [class]="tonoVigencia(l.vigencia)">{{ textoVigencia(l.vigencia, l.diasRestantes) }}</span>
                  vence el {{ formatoFecha(l.fechaFin) }}
                </span>
              </span>
              @if (puedeGestionar()) {
                <button mat-icon-button type="button" [attr.aria-label]="'Quitar ' + l.producto" [disabled]="quitando()" (click)="quitar(l)">
                  <mat-icon>link_off</mat-icon>
                </button>
              }
            </li>
          }
        </ul>
      } @else if (!datos.isLoading()) {
        <p class="atenuado vacio">No tiene licencias registradas.</p>
      }
      @if (puedeGestionar() && !deBaja()) {
        <button mat-stroked-button type="button" (click)="agregar()"><mat-icon>add</mat-icon> Agregar licencia</button>
      }
    </section>
  `,
  styles: `
    .tarjeta {
      background: var(--inv-superficie); border: 1px solid var(--inv-borde); border-radius: 10px; padding: 20px;
      display: flex; flex-direction: column; gap: 12px;
    }
    h2 { margin: 0; font-size: 16px; font-weight: 600; }
    .cuenta { font-weight: 400; }
    .lista { list-style: none; margin: 0; padding: 0; }
    .lista li { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 0; border-top: 1px solid #eef0ee; }
    .lista li:first-child { border-top: 0; padding-top: 0; }
    .datos { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
    .producto { font-weight: 500; text-decoration: none; }
    .producto:hover { text-decoration: underline; }
    .datos .atenuado { font-size: 13px; }
    .datos .pastilla { margin-right: 4px; }
    .vacio { margin: 0; }
    button[mat-stroked-button] { align-self: flex-start; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenciasActivoComponent {
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  readonly ficha = input.required<FichaActivoDto>();
  readonly puedeGestionar = input(false);

  protected readonly formatoFecha = formatoFecha;
  protected readonly textoVigencia = textoVigencia;
  protected readonly tonoVigencia = tonoVigencia;

  protected readonly datos = resource({
    params: () => ({ id: this.ficha().id }),
    loader: ({ params }) => this.api.invoke(obtenerLicenciasActivo, params),
  });

  protected readonly lista = computed(() => valorDe(this.datos) ?? []);
  protected readonly deBaja = computed(() => this.ficha().grupoEstado === 'DeBaja');
  protected readonly quitando = signal(false);

  protected async agregar(): Promise<void> {
    const f = this.ficha();
    const agregada = await abrirAgregarLicencia(this.dialogos, {
      activoId: f.id,
      sedeId: f.sedeId,
      titulo: `${f.generales.codigoPatrimonial} · ${f.generales.marca.nombre} ${f.generales.modelo}`,
      yaTiene: this.lista().map((l) => l.licenciaId),
    });
    if (agregada) {
      this.notificaciones.exito('Licencia agregada.');
      this.datos.reload();
    }
  }

  protected async quitar(l: LicenciaDelActivoDto): Promise<void> {
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Quitar ${l.producto}`,
      mensaje: 'Se libera el puesto para otro equipo.',
      confirmar: 'Quitar',
    });
    if (!confirmado) {
      return;
    }
    this.quitando.set(true);
    try {
      await this.api.invoke(quitarLicencia, { id: l.licenciaId, activoId: this.ficha().id });
      this.notificaciones.exito('Licencia quitada.');
      this.datos.reload();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.quitando.set(false);
    }
  }
}
