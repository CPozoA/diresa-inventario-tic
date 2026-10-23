import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { LogoComponent } from './logo.component';

/**
 * Diseño de las pantallas sin sesión (iniciar sesión, recuperar y restablecer contraseña):
 * panel oscuro con la marca a la izquierda y el formulario a la derecha. En el celular se apilan.
 */
@Component({
  selector: 'app-plantilla-acceso',
  imports: [LogoComponent],
  template: `
    <aside class="lateral">
      <app-logo subtitulo="Redes de Salud de Ayacucho" [grande]="true" />
      <div class="mensaje">
        <h2>{{ titulo() }}</h2>
        @if (descripcion()) {
          <p>{{ descripcion() }}</p>
        }
        <ng-content select="[lateral]" />
      </div>
      <span class="pie">Acceso solo para personal autorizado.</span>
    </aside>
    <main class="contenido">
      <div class="caja">
        <ng-content />
      </div>
    </main>
  `,
  styles: `
    :host {
      min-height: 100vh;
      display: flex;
      flex-wrap: wrap;
    }
    .lateral {
      flex: 1 1 360px;
      background: var(--inv-lateral);
      color: #fff;
      padding: 48px;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      gap: 40px;
      box-sizing: border-box;
    }
    .mensaje { display: flex; flex-direction: column; gap: 12px; max-width: 420px; }
    h2 { margin: 0; font-size: 28px; line-height: 1.2; font-weight: 600; }
    p { margin: 0; color: var(--inv-lateral-texto); font-size: 15px; line-height: 1.55; }
    .pie { color: var(--inv-lateral-atenuado); font-size: 13px; }
    .contenido {
      flex: 999 1 480px;
      min-width: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 48px 24px;
      box-sizing: border-box;
    }
    .caja { width: 100%; max-width: 420px; }
    @media (max-width: 720px) {
      .lateral { padding: 24px; gap: 20px; }
      h2 { font-size: 22px; }
      .contenido { align-items: flex-start; padding-top: 32px; }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlantillaAccesoComponent {
  readonly titulo = input.required<string>();
  readonly descripcion = input<string | null>(null);
}
