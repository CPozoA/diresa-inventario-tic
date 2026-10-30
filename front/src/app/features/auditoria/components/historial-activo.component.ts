import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { obtenerHistorialActivo } from '../../../api/fn/auditoria/obtener-historial-activo';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { valorDe } from '../../../shared/util/recurso';
import { esAlerta, fechaHora } from '../data/auditoria';

const VISIBLES = 8;

/**
 * Historial del activo en su ficha (D56): las acciones registradas sobre él (registro, ediciones, fotos, asignaciones,
 * mantenimientos, licencias), lo más reciente primero. Requiere Auditoria.Ver.
 */
@Component({
  selector: 'app-historial-activo',
  imports: [RouterLink, MatButtonModule],
  template: `
    <section class="tarjeta" aria-labelledby="titulo-historial-activo">
      <div class="encabezado">
        <h2 id="titulo-historial-activo">Historial de cambios</h2>
        <a class="ver-todo" routerLink="/auditoria" [queryParams]="{ buscar: ficha().generales.codigoPatrimonial }">Ver en Auditoría</a>
      </div>
      @if (mostradas().length > 0) {
        <ol class="lista">
          @for (r of mostradas(); track r.id) {
            <li [class.alerta]="esAlerta(r.accion)">
              <span class="accion">{{ r.accion }}</span>
              <span class="atenuado">{{ fechaHora(r.fecha) }} · {{ r.usuario ?? 'Usuario desconocido' }}</span>
            </li>
          }
        </ol>
        @if (lista().length > mostradas().length) {
          <button mat-button type="button" class="mas" (click)="todas.set(true)">Ver las {{ lista().length }} acciones</button>
        }
      } @else if (!datos.isLoading()) {
        <p class="atenuado vacio">Sin cambios registrados desde que se activó la auditoría.</p>
      }
    </section>
  `,
  styles: `
    .tarjeta {
      background: var(--inv-superficie); border: 1px solid var(--inv-borde); border-radius: 10px; padding: 20px;
      display: flex; flex-direction: column; gap: 12px;
    }
    .encabezado { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; }
    h2 { margin: 0; font-size: 16px; font-weight: 600; }
    .ver-todo { font-size: 13px; font-weight: 500; text-decoration: none; white-space: nowrap; }
    .lista { list-style: none; margin: 0; padding: 0; }
    .lista li { display: flex; flex-direction: column; gap: 2px; padding: 8px 0 8px 12px; border-left: 2px solid #dfe5f6; }
    .lista li.alerta { border-left-color: var(--inv-mal); }
    .accion { font-size: 14px; }
    .lista .atenuado { font-size: 12px; }
    .vacio { margin: 0; font-size: 13px; }
    .mas { align-self: flex-start; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistorialActivoComponent {
  private readonly api = inject(Api);

  readonly ficha = input.required<FichaActivoDto>();

  protected readonly esAlerta = esAlerta;
  protected readonly fechaHora = fechaHora;
  protected readonly todas = signal(false);

  /** Se vuelve a pedir cada vez que llega la ficha (después de editar, asignar…). */
  protected readonly datos = resource({
    params: () => ({ ficha: this.ficha() }),
    loader: ({ params }) => this.api.invoke(obtenerHistorialActivo, { id: params.ficha.id }),
  });

  protected readonly lista = computed(() => valorDe(this.datos) ?? []);
  protected readonly mostradas = computed(() => (this.todas() ? this.lista() : this.lista().slice(0, VISIBLES)));
}
