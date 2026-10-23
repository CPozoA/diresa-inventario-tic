import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export type TonoAviso = 'mal' | 'alerta' | 'info' | 'ok';

const ICONOS: Record<TonoAviso, string> = { mal: 'error', alerta: 'warning', info: 'info', ok: 'check_circle' };

/** Mensaje destacado (error, advertencia, información o confirmación). Se anuncia a los lectores de pantalla. */
@Component({
  selector: 'app-aviso',
  imports: [MatIconModule],
  template: `
    <div class="aviso" [class]="tono()" [attr.role]="tono() === 'mal' || tono() === 'alerta' ? 'alert' : 'status'">
      <mat-icon aria-hidden="true">{{ icono() }}</mat-icon>
      <span><ng-content /></span>
    </div>
  `,
  styles: `
    .aviso {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 12px 14px;
      border-radius: 8px;
      line-height: 1.45;
    }
    mat-icon {
      flex-shrink: 0;
      width: 20px;
      height: 20px;
      font-size: 20px;
    }
    .mal { background: var(--inv-mal-fondo); color: var(--inv-mal); }
    .alerta { background: var(--inv-alerta-fondo); color: var(--inv-alerta); }
    .info { background: var(--inv-info-fondo); color: var(--inv-primario-oscuro); }
    .ok { background: var(--inv-ok-fondo); color: var(--inv-ok); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AvisoComponent {
  readonly tono = input<TonoAviso>('mal');
  protected readonly icono = computed(() => ICONOS[this.tono()]);
}
