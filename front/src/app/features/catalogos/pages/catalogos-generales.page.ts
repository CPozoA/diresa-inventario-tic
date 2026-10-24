import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { CatalogoEditorComponent } from '../components/catalogo-editor.component';
import { recursoTiposCatalogo } from '../data/tipos-catalogo';

/** Catálogos comunes a todas las sedes (tipos de activo, marcas, estados…). Solo los modifica el superadministrador. */
@Component({
  selector: 'app-catalogos-generales',
  imports: [MatTabsModule, AvisoComponent, CatalogoEditorComponent],
  template: `
    <div class="pagina">
      <header class="pagina-encabezado">
        <div class="pagina-titulo">
          <h1>Catálogos generales</h1>
          <span class="atenuado">Listas comunes a todas las sedes. Un elemento desactivado deja de ofrecerse al registrar.</span>
        </div>
      </header>

      @if (tipos.error()) {
        <app-aviso>{{ mensajeError(tipos.error()) }}</app-aviso>
      } @else {
        <mat-tab-group animationDuration="0ms" mat-stretch-tabs="false" mat-align-tabs="start">
          @for (tipo of catalogos(); track tipo.codigo) {
            <mat-tab [label]="tipo.nombre">
              <app-catalogo-editor [tipo]="tipo" />
            </mat-tab>
          }
        </mat-tab-group>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogosGeneralesPage {
  protected readonly tipos = recursoTiposCatalogo();
  protected readonly catalogos = computed(() => (this.tipos.hasValue() ? this.tipos.value().filter((t) => !t.esDeSede) : []));

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }
}
