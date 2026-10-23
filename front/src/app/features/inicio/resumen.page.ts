import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { Permisos } from '../../core/auth/permisos';
import { SesionStore } from '../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../core/auth/si-tiene-permiso.directive';
import { SedeSeleccionadaStore } from '../../core/layout/sede-seleccionada.store';

/** Página de inicio. Mientras no exista el módulo de activos, muestra el estado vacío. */
@Component({
  selector: 'app-resumen',
  imports: [RouterLink, MatButtonModule, MatIconModule, SiTienePermisoDirective],
  template: `
    <div class="titulo">
      <h1>Resumen</h1>
      <span class="atenuado">{{ ambito() }}</span>
    </div>

    <section class="vacio">
      <span class="icono"><mat-icon aria-hidden="true">computer</mat-icon></span>
      <h2>Todavía no hay activos registrados</h2>
      <p class="atenuado">
        Cuando se registre el primer equipo, aquí verá los totales, las garantías por vencer y los equipos que conviene
        renovar.
      </p>
      <a *siTienePermiso="permisos.Activos.Crear" mat-flat-button routerLink="/inventario/nuevo">Registrar el primer activo</a>
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 24px; }
    .titulo { display: flex; flex-direction: column; gap: 4px; }
    h1 { margin: 0; font-size: 28px; font-weight: 600; }
    .vacio {
      background: var(--inv-superficie);
      border: 1px solid var(--inv-borde);
      border-radius: 10px;
      padding: 48px 32px;
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 12px;
    }
    .icono {
      width: 56px; height: 56px; border-radius: 12px;
      display: grid; place-items: center;
      background: var(--inv-info-fondo); color: var(--inv-primario);
    }
    .icono mat-icon { width: 28px; height: 28px; font-size: 28px; }
    h2 { margin: 0; font-size: 18px; font-weight: 600; }
    p { margin: 0 0 8px; max-width: 460px; line-height: 1.55; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResumenPage {
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly permisos = Permisos;
  protected readonly ambito = computed(() =>
    this.sesion.esSuperAdmin()
      ? (this.sedeSeleccionada.sede()?.nombre ?? 'Todas las sedes')
      : (this.sesion.sesion()?.sede?.nombre ?? ''),
  );
}
