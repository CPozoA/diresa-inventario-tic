import { ChangeDetectionStrategy, Component, computed, inject, input, output, resource } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { listarAsignacionesActivo } from '../../../api/fn/asignaciones/listar-asignaciones-activo';
import { AsignacionActivoDto } from '../../../api/models/asignacion-activo-dto';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { formatoFecha, textoConservacion } from '../../activos/data/textos-activo';
import { conservacionAsignacion, detallePersona, periodoAsignacion, ultimoMovimiento } from '../data/asignacion';
import { iniciales } from '../data/personal';
import { abrirAsignarDialog } from './asignar.dialog';
import { abrirDevolverDialog } from './devolver.dialog';

/**
 * Asignación del activo en su ficha (D51, D52): quién lo tiene, desde cuándo y en qué estado lo recibió, con Asignar,
 * Reasignar y Devolver (Activos.Editar), y el historial. Sin asignación, muestra el responsable escrito antes, si lo hay.
 */
@Component({
  selector: 'app-asignacion-activo',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  templateUrl: './asignacion-activo.component.html',
  styleUrl: './asignacion-activo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AsignacionActivoComponent {
  private readonly api = inject(Api);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  readonly ficha = input.required<FichaActivoDto>();
  readonly puedeEditar = input(false);

  /** Se asignó o se devolvió: la ficha se vuelve a cargar (cambian el estado y la conservación). */
  readonly cambio = output<void>();

  protected readonly iniciales = iniciales;
  protected readonly formatoFecha = formatoFecha;
  protected readonly textoConservacion = textoConservacion;
  protected readonly periodo = periodoAsignacion;
  protected readonly conservacion = conservacionAsignacion;

  /** Se vuelve a pedir cada vez que llega la ficha (después de asignar o devolver). */
  protected readonly historial = resource({
    params: () => ({ ficha: this.ficha() }),
    loader: ({ params }) => this.api.invoke(listarAsignacionesActivo, { id: params.ficha.id }),
  });

  protected readonly vigente = computed(() => this.ficha().asignacion ?? null);
  protected readonly anteriores = computed(() => (this.historial.value() ?? []).filter((a) => !!a.fechaDevolucion));
  protected readonly deBaja = computed(() => this.ficha().grupoEstado === 'DeBaja');

  /** Texto escrito antes del módulo de asignaciones (D52), solo mientras nadie lo tiene asignado. */
  protected readonly responsableAnterior = computed(() => (this.vigente() ? null : this.ficha().generales.responsable));

  protected detalle(a: AsignacionActivoDto): string {
    return detallePersona(a);
  }

  protected async asignar(): Promise<void> {
    const f = this.ficha();
    const asignado = await abrirAsignarDialog(this.dialogos, {
      activoId: f.id,
      titulo: this.titulo(f),
      sedeId: f.sedeId,
      conservacion: f.generales.conservacion,
      actual: this.vigente(),
      desde: ultimoMovimiento(this.historial.value() ?? (this.vigente() ? [this.vigente()!] : [])),
    });
    if (asignado) {
      this.notificaciones.exito(this.vigente() ? 'Activo reasignado.' : 'Activo asignado.');
      this.cambio.emit();
    }
  }

  protected async devolver(a: AsignacionActivoDto): Promise<void> {
    const f = this.ficha();
    const devuelto = await abrirDevolverDialog(this.dialogos, {
      activoId: f.id,
      titulo: this.titulo(f),
      persona: a.nombreCompleto,
      desde: a.fechaEntrega,
      conservacion: f.generales.conservacion,
    });
    if (devuelto) {
      this.notificaciones.exito('Devolución registrada.');
      this.cambio.emit();
    }
  }

  private titulo(f: FichaActivoDto): string {
    return `${f.generales.codigoPatrimonial} · ${f.generales.marca.nombre} ${f.generales.modelo}`;
  }
}
