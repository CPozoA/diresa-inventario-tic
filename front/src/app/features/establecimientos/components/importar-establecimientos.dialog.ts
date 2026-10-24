import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { ApiConfiguration } from '../../../api/api-configuration';
import { descargarPlantillaEstablecimientos } from '../../../api/fn/establecimientos/descargar-plantilla-establecimientos';
import { importarEstablecimientos } from '../../../api/fn/establecimientos/importar-establecimientos';
import { ResultadoImportacionDto } from '../../../api/models/resultado-importacion-dto';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';

/** Lo que devuelve el diálogo cuando la importación se guardó. */
export interface ResultadoImportarDialog {
  readonly mensaje: string;
}

const TAMANO_MAXIMO_MB = 5;

/**
 * Importa establecimientos desde Excel (docs/ARQUITECTURA.md §5.9). Si alguna fila tiene errores no se guarda nada:
 * el diálogo muestra qué filas corregir y se puede elegir el archivo corregido sin cerrarlo.
 */
@Component({
  selector: 'app-importar-establecimientos-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule, AvisoComponent],
  template: `
    <h2 mat-dialog-title>Importar establecimientos desde Excel</h2>
    <mat-dialog-content class="contenido">
      <p class="explicacion">
        Use la plantilla: una fila por establecimiento, con las columnas Id_Establecimiento, Codigo_Unico, Nombre, Categoria,
        Microred, Red, Provincia, Distrito y Ubigeo. Si un establecimiento ya existe (mismo Id_Establecimiento), se actualizan sus datos.
      </p>
      <button mat-button type="button" class="plantilla" (click)="descargarPlantilla()" [disabled]="descargando()">
        <mat-icon>download</mat-icon> {{ descargando() ? 'Descargando…' : 'Descargar la plantilla' }}
      </button>

      <div class="archivo">
        <input #selector type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" hidden (change)="elegir(selector)" />
        <button mat-stroked-button type="button" (click)="selector.click()" [disabled]="importando()">
          <mat-icon>attach_file</mat-icon> {{ archivo() ? 'Elegir otro archivo' : 'Elegir archivo' }}
        </button>
        <span class="nombre-archivo" [class.atenuado]="!archivo()">{{ archivo()?.name ?? 'Ningún archivo elegido' }}</span>
      </div>

      @if (importando()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (error(); as e) {
        <app-aviso>{{ e.mensaje }}</app-aviso>
      }

      @if (resultado(); as r) {
        @if (!r.guardado) {
          <app-aviso>
            No se guardó nada: {{ filasConError() === 1 ? '1 fila tiene' : filasConError() + ' filas tienen' }} problemas
            (de {{ r.filas }} revisadas). Corríjalas en el archivo y vuelva a importarlo.
          </app-aviso>
          <div class="errores" role="region" aria-label="Problemas encontrados" tabindex="0">
            <table class="tabla">
              <thead>
                <tr>
                  <th scope="col">Fila</th>
                  <th scope="col">Problema</th>
                </tr>
              </thead>
              <tbody>
                @for (e of r.errores; track $index) {
                  <tr>
                    <td class="mono">{{ e.fila }}</td>
                    <td>{{ e.mensaje }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="button" (click)="importar()" [disabled]="!archivo() || importando()">
        <mat-icon>upload_file</mat-icon> {{ importando() ? 'Importando…' : 'Importar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 16px; }
    .explicacion { margin: 0; line-height: 1.55; }
    .plantilla { align-self: flex-start; }
    .archivo { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .nombre-archivo { overflow-wrap: anywhere; }
    .errores { max-height: 280px; overflow: auto; border: 1px solid var(--inv-borde); border-radius: 8px; }
    .errores td:first-child { width: 1%; white-space: nowrap; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportarEstablecimientosDialog {
  private readonly api = inject(Api);
  private readonly http = inject(HttpClient);
  private readonly configuracion = inject(ApiConfiguration);
  private readonly referencia = inject<MatDialogRef<ImportarEstablecimientosDialog, ResultadoImportarDialog>>(MatDialogRef);

  protected readonly archivo = signal<File | null>(null);
  protected readonly importando = signal(false);
  protected readonly descargando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);
  protected readonly resultado = signal<ResultadoImportacionDto | null>(null);
  protected readonly filasConError = computed(() => new Set(this.resultado()?.errores.map((e) => e.fila) ?? []).size);

  protected elegir(selector: HTMLInputElement): void {
    const elegido = selector.files?.item(0) ?? null;
    selector.value = '';
    this.resultado.set(null);
    this.error.set(null);

    if (elegido && elegido.size > TAMANO_MAXIMO_MB * 1024 * 1024) {
      this.archivo.set(null);
      this.error.set({ codigo: 'Establecimientos.ArchivoMuyGrande', mensaje: `El archivo supera ${TAMANO_MAXIMO_MB} MB.`, estado: 400 });
      return;
    }

    this.archivo.set(elegido);
  }

  protected async importar(): Promise<void> {
    const archivo = this.archivo();
    if (!archivo || this.importando()) {
      return;
    }

    this.importando.set(true);
    this.error.set(null);
    this.resultado.set(null);

    try {
      const resultado = await this.api.invoke(importarEstablecimientos, { body: { archivo } });
      if (resultado.guardado) {
        this.referencia.close({ mensaje: resumen(resultado) });
        return;
      }
      this.resultado.set(resultado);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.importando.set(false);
    }
  }

  /**
   * La plantilla se pide por la API (con la sesión) como archivo. Se usa HttpClient directamente porque el contrato
   * OpenAPI no describe el contenido del archivo y el cliente generado lo trataría como respuesta vacía.
   */
  protected async descargarPlantilla(): Promise<void> {
    this.descargando.set(true);
    this.error.set(null);
    try {
      const url = this.configuracion.rootUrl.replace(/\/$/, '') + descargarPlantillaEstablecimientos.PATH;
      const contenido = await firstValueFrom(this.http.get(url, { responseType: 'blob' }));
      const enlace = document.createElement('a');
      enlace.href = URL.createObjectURL(contenido);
      enlace.download = 'plantilla-establecimientos.xlsx';
      enlace.click();
      URL.revokeObjectURL(enlace.href);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.descargando.set(false);
    }
  }
}

function resumen(r: ResultadoImportacionDto): string {
  const partes = [
    `${r.creados} ${Number(r.creados) === 1 ? 'nuevo' : 'nuevos'}`,
    `${r.actualizados} ${Number(r.actualizados) === 1 ? 'actualizado' : 'actualizados'}`,
  ];
  if (Number(r.sinCambios) > 0) {
    partes.push(`${r.sinCambios} sin cambios`);
  }
  return `Importación lista: ${partes.join(', ')}.`;
}
