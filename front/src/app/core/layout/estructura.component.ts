import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
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

/**
 * Estructura de las pantallas con sesión: menú lateral según permisos, encabezado y contenido.
 * En pantallas anchas el menú y el encabezado quedan fijos; en angostas el menú se abre como panel con el botón ☰.
 */
@Component({
  selector: 'app-estructura',
  host: { '(document:keydown.escape)': 'cerrarMenu(true)' },
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

  /** Panel del menú en pantallas angostas. En las anchas no tiene efecto visible. */
  protected readonly menuAbierto = signal(false);
  private readonly lateral = viewChild.required<ElementRef<HTMLElement>>('lateral');
  // read: ElementRef porque el botón es un componente de Material y, sin esto, se obtendría el componente.
  private readonly botonMenu = viewChild.required('botonMenu', { read: ElementRef<HTMLElement> });

  protected abrirMenu(): void {
    this.menuAbierto.set(true);
    // Lleva el foco al panel para que el teclado y los lectores de pantalla sigan dentro del menú.
    setTimeout(() => this.lateral().nativeElement.focus());
  }

  /** Cierra el panel. Con devolverFoco (Esc o ✕), el foco vuelve al botón ☰. */
  protected cerrarMenu(devolverFoco = false): void {
    if (!this.menuAbierto()) {
      return;
    }
    this.menuAbierto.set(false);
    if (devolverFoco) {
      this.botonMenu().nativeElement.focus();
    }
  }

  protected seleccionarSede(valor: string): void {
    this.sedeSeleccionada.seleccionar(valor === this.todas ? null : valor);
  }

  protected async cerrarSesion(): Promise<void> {
    await this.sesion.cerrarSesion();
    await this.router.navigate(['/', RUTAS.iniciarSesion]);
  }
}
