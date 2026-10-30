import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { debounceTime, distinctUntilChanged, firstValueFrom, map, startWith } from 'rxjs';

import { Api } from '../../../api/api';
import { listarActivos } from '../../../api/fn/activos/listar-activos';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { asignarLicencia } from '../../../api/fn/licencias/asignar-licencia';
import { ActivoResumenDto } from '../../../api/models/activo-resumen-dto';
import { FichaLicenciaDto } from '../../../api/models/ficha-licencia-dto';
import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { valorDe } from '../../../shared/util/recurso';
import { aNumero } from '../../activos/data/textos-activo';

export interface DatosAsignarEquipos {
  readonly licencia: FichaLicenciaDto;
}

/** Abre el diálogo; verdadero si se asignaron equipos. */
export async function abrirAsignarEquipos(dialogos: MatDialog, datos: DatosAsignarEquipos): Promise<boolean> {
  const referencia = dialogos.open<AsignarEquiposDialog, DatosAsignarEquipos, boolean>(AsignarEquiposDialog, {
    data: datos,
    width: '640px',
    autoFocus: 'first-tabbable',
  });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

const TAMANO_BUSQUEDA = 20;

/**
 * Asignar puestos de una licencia (D54): se buscan equipos de la red y se marcan, hasta completar los puestos libres.
 * Los que ya la tienen y los de baja no se pueden elegir. La elección se mantiene al cambiar la búsqueda.
 */
@Component({
  selector: 'app-asignar-equipos-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    AvisoComponent,
  ],
  template: `
    <h2 mat-dialog-title>Asignar equipos</h2>
    <mat-dialog-content class="contenido">
      <span class="atenuado subtitulo">{{ licencia.producto }} · {{ textoLibres() }}</span>
      @if (error(); as e) {
        <app-aviso>{{ e.mensaje }}</app-aviso>
      }

      <mat-form-field subscriptSizing="dynamic" floatLabel="always">
        <mat-label>Buscar equipos</mat-label>
        <mat-icon matPrefix aria-hidden="true">search</mat-icon>
        <input matInput autocomplete="off" placeholder="Código, serie, nombre de equipo, IP, modelo o persona" [formControl]="busqueda" />
      </mat-form-field>

      @if (elegidos().length > 0) {
        <div class="elegidos" aria-label="Elegidos">
          @for (a of elegidos(); track a.id) {
            <span class="chip">
              <span class="mono">{{ a.codigoPatrimonial }}</span>
              <button type="button" class="quitar" [attr.aria-label]="'Quitar ' + a.codigoPatrimonial" (click)="alternar(a)">
                <mat-icon>close</mat-icon>
              </button>
            </span>
          }
        </div>
      }

      @if (resultados.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <ul class="lista" aria-label="Equipos encontrados">
        @for (a of encontrados(); track a.id) {
          <li [class.inhabilitado]="motivo(a) !== null">
            <mat-checkbox [checked]="elegido(a)" [disabled]="motivo(a) !== null || (!elegido(a) && lleno())" (change)="alternar(a)">
              <span class="fila">
                <span class="linea">
                  <span class="mono codigo">{{ a.codigoPatrimonial }}</span>
                  <span>{{ a.marca }} {{ a.modelo }} · {{ a.tipoActivo }}</span>
                  @if (motivo(a); as m) {
                    <span class="pastilla neutra">{{ m }}</span>
                  }
                </span>
                <span class="atenuado">
                  {{ a.establecimiento }}
                  @if (a.responsable) {
                    · {{ a.responsable }}
                  }
                </span>
              </span>
            </mat-checkbox>
          </li>
        } @empty {
          @if (!resultados.isLoading()) {
            <li class="sin-datos">Ningún equipo coincide con la búsqueda.</li>
          }
        }
      </ul>
      @if (total() > encontrados().length) {
        <span class="atenuado pista">Se muestran {{ encontrados().length }} de {{ total() }}. Escriba para acotar la búsqueda.</span>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <span class="atenuado contador">{{ elegidos().length }} de {{ libres() }} {{ libres() === 1 ? 'puesto libre' : 'puestos libres' }}</span>
      <button mat-button type="button" mat-dialog-close>Cancelar</button>
      <button mat-flat-button type="button" [disabled]="elegidos().length === 0 || guardando()" (click)="guardar()">
        {{ guardando() ? 'Guardando…' : elegidos().length > 1 ? 'Asignar ' + elegidos().length + ' equipos' : 'Asignar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .contenido { display: flex; flex-direction: column; gap: 12px; }
    .contenido.mat-mdc-dialog-content { padding-top: 8px; }
    .subtitulo { margin-top: -8px; }
    mat-form-field { width: 100%; }
    .elegidos { display: flex; flex-wrap: wrap; gap: 6px; }
    .chip {
      display: inline-flex; align-items: center; gap: 2px; padding: 2px 4px 2px 10px; border-radius: 999px;
      background: var(--inv-info-fondo); color: var(--inv-primario-oscuro); font-size: 13px;
    }
    .quitar {
      display: grid; place-items: center; width: 22px; height: 22px; padding: 0; border: 0; border-radius: 999px;
      background: transparent; color: inherit; cursor: pointer;
      mat-icon { width: 16px; height: 16px; font-size: 16px; }
    }
    .quitar:hover { background: rgba(0, 0, 0, 0.06); }
    .lista { list-style: none; margin: 0; padding: 0; border: 1px solid var(--inv-borde); border-radius: 8px; }
    .lista li { padding: 4px 8px; border-top: 1px solid var(--inv-borde); }
    .lista li:first-child { border-top: 0; }
    .lista li.inhabilitado { background: var(--inv-fondo); }
    .fila { display: flex; flex-direction: column; gap: 2px; padding: 4px 0; }
    .linea { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; }
    .codigo { font-weight: 500; }
    .fila .atenuado { font-size: 12px; }
    .sin-datos { color: var(--inv-atenuado); padding: 16px 8px !important; text-align: center; }
    .pista { font-size: 12px; }
    .contador { margin-right: auto; font-size: 13px; padding-left: 8px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AsignarEquiposDialog {
  protected readonly licencia = inject<DatosAsignarEquipos>(MAT_DIALOG_DATA).licencia;
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<AsignarEquiposDialog, boolean>>(MatDialogRef);
  private readonly esSuperAdmin = inject(SesionStore).esSuperAdmin;

  protected readonly libres = signal(Math.max(0, (aNumero(this.licencia.puestos) ?? 0) - this.licencia.equipos.length)).asReadonly();
  private readonly yaAsignados = new Set(this.licencia.equipos.map((e) => e.activoId));

  protected readonly busqueda = new FormControl('', { nonNullable: true });
  private readonly texto = toSignal(
    this.busqueda.valueChanges.pipe(
      map((v) => v.trim()),
      debounceTime(300),
      distinctUntilChanged(),
      startWith(''),
    ),
    { initialValue: '' },
  );

  protected readonly resultados = resource({
    params: () => ({ buscar: this.texto() }),
    loader: ({ params }) =>
      this.api.invoke(listarActivos, {
        tamanoPagina: TAMANO_BUSQUEDA,
        ...(params.buscar ? { buscar: params.buscar } : {}),
        ...(this.esSuperAdmin() ? { sedeId: this.licencia.sedeId } : {}),
      }),
  });

  private readonly estados = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'estados-activo', incluirInactivos: true }),
  });

  /** Nombres de los estados del grupo "de baja". */
  private readonly estadosDeBaja = computed(
    () => new Set((valorDe(this.estados) ?? []).filter((e) => e.grupo === 'DeBaja').map((e) => e.nombre)),
  );

  /** Los que se pueden elegir primero; después, los que ya la tienen o están de baja. */
  protected readonly encontrados = computed(() => {
    const lista = valorDe(this.resultados)?.activos ?? [];
    return [...lista].sort((a, b) => Number(this.motivo(a) !== null) - Number(this.motivo(b) !== null));
  });
  protected readonly total = computed(() => aNumero(valorDe(this.resultados)?.total) ?? 0);

  private readonly seleccion = signal<ReadonlyMap<string, ActivoResumenDto>>(new Map());
  protected readonly elegidos = computed(() => [...this.seleccion().values()]);
  protected readonly lleno = computed(() => this.seleccion().size >= this.libres());
  protected readonly textoLibres = computed(() => {
    const l = this.libres();
    return l === 0 ? 'sin puestos libres' : l === 1 ? '1 puesto libre' : `${l} puestos libres`;
  });

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  /** Por qué no se puede elegir, o nulo. */
  protected motivo(a: ActivoResumenDto): string | null {
    return this.yaAsignados.has(a.id) ? 'Ya la tiene' : this.estadosDeBaja().has(a.estado) ? 'De baja' : null;
  }

  protected elegido(a: ActivoResumenDto): boolean {
    return this.seleccion().has(a.id);
  }

  protected alternar(a: ActivoResumenDto): void {
    const nueva = new Map(this.seleccion());
    if (nueva.has(a.id)) {
      nueva.delete(a.id);
    } else if (nueva.size < this.libres()) {
      nueva.set(a.id, a);
    }
    this.seleccion.set(nueva);
  }

  protected async guardar(): Promise<void> {
    if (this.elegidos().length === 0 || this.guardando()) {
      return;
    }
    this.guardando.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(asignarLicencia, { id: this.licencia.id, body: { activoIds: this.elegidos().map((a) => a.id) } });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }
}
