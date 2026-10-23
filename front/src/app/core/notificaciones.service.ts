import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

import { aErrorApi } from './http/error-api';

/** Mensajes breves al pie de la pantalla: confirmaciones de acciones y errores de la API. */
@Injectable({ providedIn: 'root' })
export class NotificacionesService {
  private readonly barra = inject(MatSnackBar);

  exito(texto: string): void {
    this.barra.open(texto, 'Cerrar', { duration: 4000 });
  }

  /** Muestra el mensaje en español que envía la API (o uno genérico si el error es inesperado). */
  error(error: unknown): void {
    this.barra.open(aErrorApi(error).mensaje, 'Cerrar', { duration: 8000, panelClass: 'notificacion-error' });
  }
}
