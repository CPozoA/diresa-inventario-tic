import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { asignarLicencia } from '../../../api/fn/licencias/asignar-licencia';
import { listarLicencias } from '../../../api/fn/licencias/listar-licencias';
import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { valorDe } from '../../../shared/util/recurso';
import { formatoFecha } from '../../activos/data/textos-activo';
import { textoLibres, textoVigencia, tonoVigencia } from '../data/licencias';

export interface DatosAgregarLicencia {
  readonly activoId: string;
  readonly sedeId: string;
  /** "PAT-0001 · Dell Latitude 5440". */
  readonly titulo: string;
  /** Licencias que el activo ya tiene. */
  readonly yaTiene: readonly string[];
}

/** Abre el diálogo; verdadero si se agregó la licencia. */
export async function abrirAgregarLicencia(dialogos: MatDialog, datos: DatosAgregarLicencia): Promise<boolean> {
  const referencia = dialogos.open<AgregarLicenciaDialog, DatosAgregarLicencia, boolean>(AgregarLicenciaDialog, {
    data: datos,
    width: '560px',
  });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

/** Desde la ficha del activo: elegir una licencia vigente con puestos libres y asignársela (D54). */
@Component({
  selector: 'app-agregar-licencia-dialog',
  imports: [MatButtonModule, MatDialogModule, MatProgressBarModule, MatRadioModule, RouterLink, AvisoComponent],
  template: `
    <h2 mat-dialog-title>Agregar licencia</h2>
    <mat-dialog-content class="contenido">
      <span class="atenuado subtitulo">{{ datos.titulo }}</span>
      @if (error(); as e) {
        <app-aviso>{{ e.mensaje }}</app-aviso>
      }
      @if (licencias.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (disponibles().length > 0) {
        <mat-radio-group class="lista" aria-label="Licencias vigentes" [value]="elegida()" (change)="elegida.set($event.value)">
          @for (l of disponibles(); track l.id) {
            <mat-radio-button [value]="l.id" [disabled]="!conLibres(l)">
              <span class="fila">
                <span class="producto">{{ l.producto }}</span>
                <span class="atenuado">
                  <span class="pastilla" [class]="tonoVigencia(l.vigencia)">{{ textoVigencia(l.vigencia, l.diasRestantes) }}</span>
                  vence el {{ formatoFecha(l.fechaFin) }} · {{ textoLibres(l.equipos, l.puestos) }}
                </span>
              </span>
            </mat-radio-button>
          }
        </mat-radio-group>
      } @else if (!licencias.isLoading()) {
        <p class="atenuado vacio">
          No hay licencias vigentes que este equipo no tenga ya.
          <a routerLink="/licencias" mat-dialog-close>Ir a Licencias</a> para registrar una.
        </p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="button" [disabled]="!elegida() || guardando()" (click)="guardar()">
        {{ guardando() ? 'Guardando…' : 'Agregar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 12px; }
    .contenido.mat-mdc-dialog-content { padding-top: 8px; }
    .subtitulo { margin-top: -8px; }
    .lista { display: flex; flex-direction: column; border: 1px solid var(--inv-borde); border-radius: 8px; }
    .lista mat-radio-button { padding: 4px 8px; border-top: 1px solid var(--inv-borde); }
    .lista mat-radio-button:first-child { border-top: 0; }
    .fila { display: flex; flex-direction: column; gap: 4px; padding: 6px 0; }
    .producto { font-weight: 500; }
    .fila .atenuado { font-size: 12px; }
    .fila .pastilla { margin-right: 4px; }
    .vacio { margin: 8px 0; line-height: 1.5; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgregarLicenciaDialog {
  protected readonly datos = inject<DatosAgregarLicencia>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<AgregarLicenciaDialog, boolean>>(MatDialogRef);
  private readonly esSuperAdmin = inject(SesionStore).esSuperAdmin;

  protected readonly formatoFecha = formatoFecha;
  protected readonly textoVigencia = textoVigencia;
  protected readonly tonoVigencia = tonoVigencia;
  protected readonly textoLibres = textoLibres;

  protected readonly licencias = resource({
    loader: () =>
      this.api.invoke(listarLicencias, { filtro: 'Vigentes', ...(this.esSuperAdmin() ? { sedeId: this.datos.sedeId } : {}) }),
  });

  /** Vigentes que el equipo no tiene; las que tienen puestos libres primero. */
  protected readonly disponibles = computed(() =>
    (valorDe(this.licencias) ?? [])
      .filter((l) => !this.datos.yaTiene.includes(l.id))
      .sort((a, b) => Number(this.conLibres(b)) - Number(this.conLibres(a))),
  );

  protected readonly elegida = signal<string | null>(null);
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected conLibres(l: { equipos: number | string; puestos: number | string }): boolean {
    return Number(l.equipos) < Number(l.puestos);
  }

  protected async guardar(): Promise<void> {
    const id = this.elegida();
    if (!id || this.guardando()) {
      return;
    }
    this.guardando.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(asignarLicencia, { id, body: { activoIds: [this.datos.activoId] } });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
