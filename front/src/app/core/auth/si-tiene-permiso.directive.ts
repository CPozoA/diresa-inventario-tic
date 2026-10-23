import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { CodigoPermiso } from './permisos';
import { SesionStore } from './sesion.store';

/**
 * Muestra el elemento solo si el usuario tiene alguno de los permisos indicados.
 * Uso: `<button *siTienePermiso="Permisos.Usuarios.Gestionar">Nuevo usuario</button>`
 * Es solo presentación: la API vuelve a validar cada acción.
 */
@Directive({ selector: '[siTienePermiso]' })
export class SiTienePermisoDirective {
  readonly siTienePermiso = input.required<CodigoPermiso | readonly CodigoPermiso[]>();

  private readonly plantilla = inject(TemplateRef<unknown>);
  private readonly contenedor = inject(ViewContainerRef);
  private readonly sesion = inject(SesionStore);
  private visible = false;

  constructor() {
    effect(() => {
      const valor = this.siTienePermiso();
      const permisos: readonly CodigoPermiso[] = typeof valor === 'string' ? [valor] : valor;
      const debeVerse = this.sesion.tieneAlguno(permisos);

      if (debeVerse && !this.visible) {
        this.contenedor.createEmbeddedView(this.plantilla);
      } else if (!debeVerse && this.visible) {
        this.contenedor.clear();
      }
      this.visible = debeVerse;
    });
  }
}
