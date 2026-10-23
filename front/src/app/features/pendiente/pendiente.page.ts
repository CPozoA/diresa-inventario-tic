import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Lugar de las funcionalidades que aún no se construyen (sedes, usuarios y roles llegan en el Paso 6;
 * activos en el 7). Así el menú y los permisos ya se pueden probar de punta a punta.
 */
@Component({
  selector: 'app-pendiente',
  imports: [MatIconModule],
  template: `
    <h1>{{ titulo() }}</h1>
    <section class="vacio">
      <mat-icon aria-hidden="true">construction</mat-icon>
      <p>Esta sección estará disponible en una próxima versión.</p>
    </section>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 24px; }
    h1 { margin: 0; font-size: 28px; font-weight: 600; }
    .vacio {
      background: var(--inv-superficie);
      border: 1px dashed var(--inv-borde-campo);
      border-radius: 10px;
      padding: 40px 32px;
      display: flex;
      align-items: center;
      gap: 12px;
      color: var(--inv-atenuado);
    }
    p { margin: 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendientePage {
  /** Viene de data.titulo de la ruta. */
  readonly titulo = input('');
}
