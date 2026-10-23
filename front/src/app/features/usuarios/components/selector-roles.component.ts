import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';

import { RolDto } from '../../../api/models/rol-dto';
import { puedeOtorgarRol } from '../../../core/auth/reglas-gestion';
import { SesionStore } from '../../../core/auth/sesion.store';

/** Lista de roles para marcar. Los que quien asigna no puede otorgar aparecen deshabilitados. */
@Component({
  selector: 'app-selector-roles',
  imports: [MatCheckboxModule],
  template: `
    <fieldset>
      <legend>Roles</legend>
      @for (opcion of opciones(); track opcion.rol.id) {
        <div class="opcion">
          <mat-checkbox
            [checked]="seleccion().includes(opcion.rol.id)"
            [disabled]="!opcion.otorgable"
            (change)="alternar(opcion.rol.id, $event.checked)"
          >
            <span class="nombre">{{ opcion.rol.nombre }}</span>
          </mat-checkbox>
          <span class="detalle">
            @if (!opcion.otorgable) {
              {{ opcion.rol.esDelSistema ? 'Solo lo asigna el superadministrador.' : 'Tiene permisos que usted no tiene.' }}
            } @else {
              {{ opcion.rol.descripcion ?? opcion.rol.permisos.length + ' permisos' }}
            }
          </span>
        </div>
      } @empty {
        <p class="atenuado">No hay roles disponibles.</p>
      }
    </fieldset>
  `,
  styles: `
    fieldset {
      margin: 0;
      padding: 8px 12px 12px;
      border: 1px solid var(--inv-borde);
      border-radius: 8px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    legend { padding: 0 4px; font-weight: 600; font-size: 13px; }
    .opcion { display: flex; flex-direction: column; }
    .nombre { font-weight: 500; }
    .detalle { margin: -6px 0 4px 40px; color: var(--inv-atenuado); font-size: 12px; }
    p { margin: 4px 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectorRolesComponent {
  private readonly sesion = inject(SesionStore);

  readonly roles = input.required<readonly RolDto[]>();

  /** Identificadores de los roles marcados (enlace en dos sentidos). */
  readonly seleccion = model.required<string[]>();

  protected readonly opciones = computed(() =>
    this.roles().map((rol) => ({
      rol,
      otorgable: puedeOtorgarRol(rol, this.sesion.permisos(), this.sesion.esSuperAdmin()),
    })),
  );

  protected alternar(rolId: string, marcado: boolean): void {
    const actual = this.seleccion().filter((id) => id !== rolId);
    this.seleccion.set(marcado ? [...actual, rolId] : actual);
  }
}
