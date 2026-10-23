import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { Permisos } from '../auth/permisos';
import { SesionStore } from '../auth/sesion.store';
import { SiTienePermisoDirective } from '../auth/si-tiene-permiso.directive';
import { RUTAS } from '../rutas';
import { LogoComponent } from '../../shared/ui/logo.component';
import { menuVisible } from './menu';
import { SedeSeleccionadaStore } from './sede-seleccionada.store';

/** Estructura de las pantallas con sesión: menú lateral según permisos, encabezado y contenido. */
@Component({
  selector: 'app-estructura',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatButtonModule,
    MatDividerModule,
    MatIconModule,
    MatMenuModule,
    MatSelectModule,
    LogoComponent,
    SiTienePermisoDirective,
  ],
  templateUrl: './estructura.component.html',
  styleUrl: './estructura.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstructuraComponent {
  protected readonly sesion = inject(SesionStore);
  protected readonly sedeSeleccionada = inject(SedeSeleccionadaStore);
  private readonly router = inject(Router);

  protected readonly permisos = Permisos;
  protected readonly rutas = RUTAS;
  protected readonly grupos = computed(() => menuVisible(this.sesion.permisos()));

  protected readonly nombreSede = computed(() => {
    if (this.sesion.esSuperAdmin()) {
      return this.sedeSeleccionada.sede()?.nombre ?? 'Todas las sedes';
    }
    return this.sesion.sesion()?.sede?.nombre ?? '';
  });

  protected readonly rol = computed(() => (this.sesion.esSuperAdmin() ? 'Superadministrador' : this.nombreSede()));

  protected readonly iniciales = computed(() => {
    const nombre = this.sesion.sesion()?.nombreCompleto ?? '';
    const partes = nombre.split(/\s+/).filter(Boolean);
    return partes.length === 0 ? '?' : (partes[0][0] + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase();
  });

  constructor() {
    if (this.sesion.esSuperAdmin()) {
      void this.sedeSeleccionada.cargarSedes();
    }
  }

  /** Valor del selector para "Todas las sedes" (mat-select no muestra el texto de una opción con valor nulo). */
  protected readonly todas = 'todas';

  protected seleccionarSede(valor: string): void {
    this.sedeSeleccionada.seleccionar(valor === this.todas ? null : valor);
  }

  protected async cerrarSesion(): Promise<void> {
    await this.sesion.cerrarSesion();
    await this.router.navigate(['/', RUTAS.iniciarSesion]);
  }
}
