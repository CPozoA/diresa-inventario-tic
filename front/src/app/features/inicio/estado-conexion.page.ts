import { ChangeDetectionStrategy, Component, inject, resource } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { Api } from '../../api/api';
import { obtenerSalud } from '../../api/fn/salud/obtener-salud';
import { SesionStore } from '../../core/auth/sesion.store';
import { aErrorApi } from '../../core/http/error-api';

/**
 * Página provisional del Paso 5A: comprueba que el frontend llega a la API a través del proxy
 * y que la sesión se puede consultar. En el Paso 5B la reemplaza el inicio de sesión y la estructura con menú.
 */
@Component({
  selector: 'app-estado-conexion',
  imports: [DatePipe, MatButtonModule, MatCardModule, MatIconModule, MatProgressBarModule],
  templateUrl: './estado-conexion.page.html',
  styleUrl: './estado-conexion.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EstadoConexionPage {
  private readonly api = inject(Api);
  protected readonly sesion = inject(SesionStore);

  protected readonly salud = resource({ loader: () => this.api.invoke(obtenerSalud) });

  protected mensajeError(error: unknown): string {
    return aErrorApi(error).mensaje;
  }

  constructor() {
    void this.sesion.asegurarVerificada();
  }

  protected reintentar(): void {
    this.salud.reload();
    void this.sesion.recargar();
  }
}
