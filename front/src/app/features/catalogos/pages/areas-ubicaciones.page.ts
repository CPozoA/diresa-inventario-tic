import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { SesionStore } from '../../../core/auth/sesion.store';
import { aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { CatalogoEditorComponent } from '../components/catalogo-editor.component';
import { recursoTiposCatalogo } from '../data/tipos-catalogo';

/** Áreas y ubicaciones de la sede. El superadministrador elige la sede en el encabezado. */
@Component({
  selector: 'app-areas-ubicaciones',
  imports: [MatTabsModule, AvisoComponent, CatalogoEditorComponent],
  template: `
    <div class="pagina">
      <header class="pagina-encabezado">
        <div class="pagina-titulo">
          <h1>Áreas y ubicaciones</h1>
          <span class="atenuado">{{ ambito() || 'Elija una sede' }}</span>
        </div>
      </header>

      @if (faltaSede()) {
        <app-aviso tono="info">Las áreas y ubicaciones son de cada sede. Elija una sede en el selector de la parte superior.</app-aviso>
      } @else if (tipos.error()) {
        <app-aviso>{{ mensajeError(tipos.error()) }}</app-aviso>
      } @else {
        <mat-tab-group animationDuration="0ms" mat-stretch-tabs="false" mat-align-tabs="start">
          @for (tipo of catalogos(); track tipo.codigo) {
            <mat-tab [label]="tipo.nombre">
              <app-catalogo-editor [tipo]="tipo" [sedeId]="sedeId()" />
            </mat-tab>
          }
        </mat-tab-group>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AreasUbicacionesPage {
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly tipos = recursoTiposCatalogo();
  protected readonly catalogos = computed(() => (this.tipos.hasValue() ? this.tipos.value().filter((t) => t.esDeSede) : []));

  protected readonly sedeId = computed(() => (this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));
  protected readonly faltaSede = computed(() => this.sesion.esSuperAdmin() && this.sedeSeleccionada.sedeId() === null);
  protected readonly ambito = computed(() =>
    this.sesion.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? '') : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }
}
