import { Injectable, inject } from '@angular/core';

import { Api } from '../../api/api';
import { obtenerTokenCsrf } from '../../api/fn/cuenta/obtener-token-csrf';

/**
 * Token antifalsificación (protección CSRF, docs/ARQUITECTURA.md D24).
 * La API lo liga al usuario: se pide al arrancar y de nuevo después de iniciar o cerrar sesión.
 */
@Injectable({ providedIn: 'root' })
export class CsrfService {
  private readonly api = inject(Api);

  private token: string | null = null;
  private pendiente: Promise<string> | null = null;

  /** Devuelve el token vigente; si aún no hay uno, lo pide. */
  obtener(): Promise<string> {
    return this.token !== null ? Promise.resolve(this.token) : this.renovar();
  }

  /** Pide un token nuevo. Si ya hay un pedido en curso, lo reutiliza. */
  renovar(): Promise<string> {
    this.pendiente ??= this.api
      .invoke(obtenerTokenCsrf)
      .then((respuesta) => {
        const token = respuesta.token;
        if (!token) {
          throw new Error('La API no devolvió el token antifalsificación.');
        }
        this.token = token;
        return token;
      })
      .finally(() => {
        this.pendiente = null;
      });

    return this.pendiente;
  }
}
