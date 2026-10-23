import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Logo de la aplicación: el distintivo "TI" con el nombre y un subtítulo opcional. */
@Component({
  selector: 'app-logo',
  template: `
    <span class="distintivo" [class.grande]="grande()" aria-hidden="true">TI</span>
    <span class="textos">
      <span class="nombre" [class.grande]="grande()">Inventario TIC</span>
      @if (subtitulo()) {
        <span class="subtitulo">{{ subtitulo() }}</span>
      }
    </span>
  `,
  styles: `
    :host { display: flex; align-items: center; gap: 10px; min-width: 0; }
    .distintivo {
      width: 32px; height: 32px; border-radius: 6px; flex-shrink: 0;
      display: grid; place-items: center;
      background: var(--inv-acento); color: var(--inv-tinta);
      font-family: var(--inv-fuente-mono); font-weight: 500; font-size: 13px;
    }
    .distintivo.grande { width: 40px; height: 40px; border-radius: 8px; font-size: 15px; }
    .textos { display: flex; flex-direction: column; min-width: 0; }
    .nombre { color: #fff; font-weight: 600; font-size: 15px; }
    .nombre.grande { font-size: 17px; }
    .subtitulo { color: var(--inv-lateral-atenuado); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoComponent {
  readonly subtitulo = input<string | null>(null);
  readonly grande = input(false);
}
