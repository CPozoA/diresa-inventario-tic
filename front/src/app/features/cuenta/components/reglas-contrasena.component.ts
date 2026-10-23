import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { ReglaContrasena } from '../data/reglas-contrasena';

/** Lista de requisitos de la contraseña, que se marcan en verde a medida que se cumplen. */
@Component({
  selector: 'app-reglas-contrasena',
  imports: [MatIconModule],
  template: `
    <ul aria-label="Requisitos de la contraseña">
      @for (regla of reglas(); track regla.texto) {
        <li [class.cumple]="regla.cumple">
          <mat-icon aria-hidden="true">{{ regla.cumple ? 'check' : 'radio_button_unchecked' }}</mat-icon>
          <span>{{ regla.texto }}</span>
          <span class="visualmente-oculto">{{ regla.cumple ? '(cumple)' : '(falta)' }}</span>
        </li>
      }
    </ul>
  `,
  styles: `
    ul {
      margin: 0;
      padding: 14px 16px;
      list-style: none;
      background: var(--inv-fondo);
      border-radius: 8px;
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 8px 16px;
      font-size: 13px;
    }
    li { display: flex; align-items: center; gap: 8px; color: var(--inv-atenuado); }
    li.cumple { color: var(--inv-ok); }
    mat-icon { width: 16px; height: 16px; font-size: 16px; flex-shrink: 0; }
    @media (max-width: 480px) {
      ul { grid-template-columns: minmax(0, 1fr); }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReglasContrasenaComponent {
  readonly reglas = input.required<readonly ReglaContrasena[]>();
}
