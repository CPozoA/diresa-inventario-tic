import { ChangeDetectionStrategy, Component, inject, resource } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { Api } from '../../api/api';
import { listarEstablecimientos } from '../../api/fn/establecimientos/listar-establecimientos';
import { aErrorApi } from '../../core/http/error-api';
import { AvisoComponent } from '../../shared/ui/aviso.component';

export interface DatosSinActivos {
  /** Nula: la red del usuario. */
  readonly sedeId: string | null;
  readonly red: string;
}

export function abrirEstablecimientosSinActivos(dialogos: MatDialog, datos: DatosSinActivos): void {
  dialogos.open(EstablecimientosSinActivosDialog, { data: datos, width: '560px', maxWidth: '94vw', autoFocus: 'dialog' });
}

/** Establecimientos vigentes de la red donde todavía no se registró ningún activo (resumen del inicio). */
@Component({
  selector: 'app-establecimientos-sin-activos',
  imports: [MatDialogModule, MatButtonModule, MatProgressBarModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>Establecimientos sin activos</h2>
    <mat-dialog-content>
      <p class="atenuado">{{ datos.red }}: establecimientos vigentes donde todavía no se registró ningún equipo.</p>
      @if (lista.isLoading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando" />
      } @else if (lista.error()) {
        <app-aviso>{{ error() }}</app-aviso>
      } @else {
        <ul>
          @for (e of lista.value() ?? []; track e.id) {
            <li>
              <span class="nombre">{{ e.nombre }}</span>
              <span class="atenuado">Cód. único {{ e.codigoUnico }}{{ e.microred ? ' · ' + e.microred : '' }}</span>
            </li>
          } @empty {
            <li class="atenuado">Todos los establecimientos tienen al menos un activo.</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button type="button" mat-dialog-close>Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    p { margin: 0 0 12px; }
    ul { list-style: none; margin: 0; padding: 0; }
    li { display: flex; flex-direction: column; gap: 2px; padding: 10px 0; border-top: 1px solid var(--inv-borde); }
    .nombre { color: var(--inv-tinta); font-weight: 500; }
    .atenuado { color: var(--inv-atenuado); font-size: 13px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstablecimientosSinActivosDialog {
  private readonly api = inject(Api);
  protected readonly datos = inject<DatosSinActivos>(MAT_DIALOG_DATA);

  protected readonly lista = resource({
    loader: () =>
      this.api.invoke(listarEstablecimientos, { sinActivos: true, ...(this.datos.sedeId ? { sedeId: this.datos.sedeId } : {}) }),
  });

  protected error(): string {
    return aErrorApi(this.lista.error()).mensaje;
  }
}
