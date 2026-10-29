import { ChangeDetectionStrategy, Component, computed, inject, input, output, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

import { Api } from '../../../api/api';
import { obtenerMantenimientosActivo } from '../../../api/fn/mantenimiento/obtener-mantenimientos-activo';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { hoyIso } from '../../activos/data/formulario-activo';
import { formatoFecha } from '../../activos/data/textos-activo';
import {
  cuandoToca,
  fechasMantenimiento,
  haceCuanto,
  quien,
  textoCosto,
  textoResultado,
  textoTipo,
  tonoResultado,
  tonoTipo,
} from '../data/mantenimiento';
import { abrirCerrarMantenimiento } from './cerrar-mantenimiento.dialog';
import { abrirRegistrarMantenimiento } from './registrar-mantenimiento.dialog';
import { valorDe } from '../../../shared/util/recurso';

/**
 * Mantenimiento del activo en su ficha (D53): el en curso (con "Cerrar mantenimiento"), el último y el próximo
 * preventivo, "Registrar mantenimiento" e historial. Registrar y cerrar requieren Mantenimiento.Gestionar.
 */
@Component({
  selector: 'app-mantenimiento-activo',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './mantenimiento-activo.component.html',
  styleUrl: './mantenimiento-activo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MantenimientoActivoComponent {
  protected readonly valorDe = valorDe;
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  readonly ficha = input.required<FichaActivoDto>();
  readonly puedeGestionar = input(false);

  /** Se registró o cerró: la ficha se vuelve a cargar (cambia el estado del activo). */
  readonly cambio = output<void>();

  protected readonly hoy = hoyIso();
  protected readonly formatoFecha = formatoFecha;
  protected readonly textoTipo = textoTipo;
  protected readonly tonoTipo = tonoTipo;
  protected readonly textoResultado = textoResultado;
  protected readonly tonoResultado = tonoResultado;
  protected readonly fechas = fechasMantenimiento;
  protected readonly quien = quien;
  protected readonly textoCosto = textoCosto;
  protected readonly haceCuanto = haceCuanto;

  /** Se vuelve a pedir cada vez que llega la ficha. */
  protected readonly datos = resource({
    params: () => ({ ficha: this.ficha() }),
    loader: ({ params }) => this.api.invoke(obtenerMantenimientosActivo, { id: params.ficha.id }),
  });

  protected readonly enCurso = computed(() => valorDe(this.datos)?.enCurso ?? null);
  protected readonly historial = computed(() => valorDe(this.datos)?.historial ?? []);
  protected readonly proximo = computed(() => {
    const p = valorDe(this.datos)?.proximoMantenimiento;
    return p ? { fecha: p, ...cuandoToca(p, this.hoy) } : null;
  });
  protected readonly deBaja = computed(() => this.ficha().grupoEstado === 'DeBaja');

  protected async registrar(): Promise<void> {
    if (await abrirRegistrarMantenimiento(this.dialogos, { activoId: this.ficha().id })) {
      this.notificaciones.exito('Mantenimiento registrado.');
      this.cambio.emit();
    }
  }

  protected async cerrar(): Promise<void> {
    const m = this.enCurso();
    if (m && (await abrirCerrarMantenimiento(this.dialogos, m))) {
      this.notificaciones.exito('Mantenimiento cerrado.');
      this.cambio.emit();
    }
  }
}
