import { ChangeDetectionStrategy, Component, computed, inject, input, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { obtenerCifrasLicencias } from '../../../api/fn/licencias/obtener-cifras-licencias';
import { FiltroLicencias } from '../../../api/models/filtro-licencias';
import { valorDe } from '../../../shared/util/recurso';
import { aNumero } from '../../activos/data/textos-activo';

interface Alerta {
  readonly texto: string;
  readonly filtro: NonNullable<FiltroLicencias>;
  readonly tono: 'mal' | 'alerta';
}

/**
 * Avisos de licencias en el resumen (D54): vencidas, por vencer y con más equipos que puestos. Cada uno abre la lista
 * filtrada. No muestra nada si no hay avisos.
 */
@Component({
  selector: 'app-alertas-licencias',
  imports: [RouterLink, MatIconModule],
  template: `
    @if (alertas().length > 0) {
      <ul class="alertas" aria-label="Licencias">
        @for (a of alertas(); track a.filtro) {
          <li>
            <a class="aviso-licencia" [class]="a.tono" routerLink="/licencias" [queryParams]="{ filtro: a.filtro }">
              <mat-icon aria-hidden="true">key</mat-icon>
              <span>{{ a.texto }}</span>
              <mat-icon class="ir" aria-hidden="true">chevron_right</mat-icon>
            </a>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .alertas { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 6px; }
    .aviso-licencia {
      display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: 8px; text-decoration: none;
      font-size: 14px; font-weight: 500;
    }
    .aviso-licencia mat-icon { width: 18px; height: 18px; font-size: 18px; flex: 0 0 auto; }
    .aviso-licencia .ir { margin-left: auto; }
    .aviso-licencia.mal { background: var(--inv-mal-fondo); color: var(--inv-mal); }
    .aviso-licencia.alerta { background: var(--inv-alerta-fondo); color: var(--inv-alerta); }
    .aviso-licencia:hover span { text-decoration: underline; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertasLicenciasComponent {
  private readonly api = inject(Api);

  /** Red (superadministrador); nula: la del usuario o todas. */
  readonly sedeId = input<string | null>(null);

  private readonly cifras = resource({
    params: () => ({ sedeId: this.sedeId() }),
    loader: ({ params }) => this.api.invoke(obtenerCifrasLicencias, params.sedeId ? { sedeId: params.sedeId } : {}),
  });

  protected readonly alertas = computed<Alerta[]>(() => {
    const c = valorDe(this.cifras);
    if (!c) {
      return [];
    }
    const vencidas = aNumero(c.vencidas) ?? 0;
    const porVencer = aNumero(c.porVencer) ?? 0;
    const excedidas = aNumero(c.excedidas) ?? 0;
    const dias = aNumero(c.dias) ?? 30;
    const lista: Alerta[] = [];
    if (vencidas > 0) {
      lista.push({ texto: vencidas === 1 ? '1 licencia vencida' : `${vencidas} licencias vencidas`, filtro: 'Vencidas', tono: 'mal' });
    }
    if (porVencer > 0) {
      lista.push({
        texto: porVencer === 1 ? `1 licencia vence en los próximos ${dias} días` : `${porVencer} licencias vencen en los próximos ${dias} días`,
        filtro: 'PorVencer',
        tono: 'alerta',
      });
    }
    if (excedidas > 0) {
      lista.push({
        texto: excedidas === 1 ? '1 licencia con más equipos que puestos' : `${excedidas} licencias con más equipos que puestos`,
        filtro: 'Excedidas',
        tono: 'alerta',
      });
    }
    return lista;
  });
}
