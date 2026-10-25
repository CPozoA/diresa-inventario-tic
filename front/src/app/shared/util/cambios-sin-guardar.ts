import { CanDeactivateFn } from '@angular/router';

/** Pantalla con un formulario que puede tener cambios sin guardar. */
export interface ConCambiosSinGuardar {
  /** Verdadero si se puede salir (no hay cambios, o el usuario confirmó que los descarta). */
  puedeSalir(): boolean | Promise<boolean>;
}

/** Pregunta antes de salir de un formulario con cambios sin guardar (enlaces del menú, Cancelar, botón Atrás). */
export const cambiosSinGuardarGuard: CanDeactivateFn<ConCambiosSinGuardar> = (pantalla) => pantalla.puedeSalir();
