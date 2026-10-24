import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { SesionStore } from '../../../core/auth/sesion.store';
import { aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { CatalogoEditorComponent } from '../components/catalogo-editor.component';
import { recursoTiposCatalogo } from '../data/tipos-catalogo';

/**
 * Áreas o dependencias de la sede. El superadministrador elige la sede en el encabezado.
 * La ubicación física ya no es un catálogo: se escribe en cada activo (docs/ARQUITECTURA.md D40).
 */
@Component({
  selector: 'app-areas',
  imports: [AvisoComponent, CatalogoEditorComponent],
  template: `
    <div class="pagina">
      <header class="pagina-encabezado">
        <div class="pagina-titulo">
          <h1>Áreas</h1>
          <span class="atenuado">{{ ambito() || 'Elija una sede' }}</span>
        </div>
      </header>

      @if (faltaSede()) {
        <app-aviso tono="info">Las áreas son de cada sede. Elija una sede en el selector de la parte superior.</app-aviso>
      } @else if (tipos.error()) {
        <app-aviso>{{ mensajeError(tipos.error()) }}</app-aviso>
      } @else if (areas(); as tipo) {
        <app-catalogo-editor [tipo]="tipo" [sedeId]="sedeId()" />
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AreasPage {
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly tipos = recursoTiposCatalogo();
  protected readonly areas = computed(() => (this.tipos.hasValue() ? this.tipos.value().find((t) => t.codigo === 'areas') : undefined));

  protected readonly sedeId = computed(() => (this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null));
  protected readonly faltaSede = computed(() => this.sesion.esSuperAdmin() && this.sedeSeleccionada.sedeId() === null);
  protected readonly ambito = computed(() =>
    this.sesion.esSuperAdmin() ? (this.sedeSeleccionada.sede()?.nombre ?? '') : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }
}
