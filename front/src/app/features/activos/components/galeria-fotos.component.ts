import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { Api } from '../../../api/api';
import { ApiConfiguration } from '../../../api/api-configuration';
import { subirFotoActivo } from '../../../api/fn/fotos-de-activos/subir-foto-activo';
import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import { aErrorApi } from '../../../core/http/error-api';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { MAXIMO_FOTOS, nombreFoto, urlFoto } from '../data/fotos';
import { ErrorImagen, reducirImagen } from '../data/reducir-imagen';
import { formatoFecha } from '../data/textos-activo';
import { abrirVisorFotos } from './visor-fotos.dialog';

/**
 * Fotos en la ficha del activo (D38): la elegida en grande, las demás en miniatura y "Agregar", que sube al momento.
 * Al tocar la foto grande se abre el visor, donde se marca la principal, se describe o se elimina.
 */
@Component({
  selector: 'app-galeria-fotos',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './galeria-fotos.component.html',
  styleUrl: './galeria-fotos.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GaleriaFotosComponent {
  private readonly api = inject(Api);
  private readonly raiz = inject(ApiConfiguration).rootUrl;
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);

  readonly activoId = input.required<string>();
  /** Código patrimonial y equipo, para el título del visor. */
  readonly titulo = input.required<string>();
  /** Ya ordenadas: la principal primero. */
  readonly fotos = input.required<readonly FotoActivoDto[]>();
  readonly cargando = input(false);
  readonly puedeEditar = input(false);

  /** Algo cambió en la API: hay que volver a pedir las fotos. */
  readonly cambiaron = output<void>();

  protected readonly maximo = MAXIMO_FOTOS;
  protected readonly nombreFoto = nombreFoto;
  protected readonly formatoFecha = formatoFecha;

  private readonly elegidaId = signal<string | null>(null);
  protected readonly subiendo = signal<{ hechas: number; total: number } | null>(null);

  protected readonly indice = computed(() => {
    const i = this.fotos().findIndex((f) => f.id === this.elegidaId());
    return i >= 0 ? i : 0;
  });
  protected readonly actual = computed(() => this.fotos()[this.indice()] ?? null);
  protected readonly lugares = computed(() => MAXIMO_FOTOS - this.fotos().length);

  protected url(foto: FotoActivoDto, miniatura: boolean): string {
    return urlFoto(this.raiz, this.activoId(), foto.id, miniatura);
  }

  protected elegir(foto: FotoActivoDto): void {
    this.elegidaId.set(foto.id);
  }

  protected abrirVisor(): void {
    abrirVisorFotos(this.dialogos, {
      activoId: this.activoId(),
      titulo: this.titulo(),
      fotos: this.fotos(),
      indice: this.indice(),
      puedeEditar: this.puedeEditar(),
      alCambiar: () => this.cambiaron.emit(),
    });
  }

  /** Reduce y sube al momento, una por una. */
  protected async agregar(evento: Event): Promise<void> {
    const entrada = evento.target as HTMLInputElement;
    const elegidos = Array.from(entrada.files ?? []);
    entrada.value = '';
    const archivos = elegidos.slice(0, Math.max(0, this.lugares()));
    if (elegidos.length > archivos.length) {
      this.notificaciones.aviso(
        `Solo caben ${MAXIMO_FOTOS} fotos por activo: ${archivos.length === 0 ? 'no se agregó ninguna' : `se agregarán ${archivos.length}`}.`,
      );
    }
    if (archivos.length === 0) {
      return;
    }

    this.subiendo.set({ hechas: 0, total: archivos.length });
    try {
      for (const archivo of archivos) {
        try {
          const imagen = await reducirImagen(archivo);
          const subida = await this.api.invoke(subirFotoActivo, {
            id: this.activoId(),
            body: { archivo: imagen.foto, miniatura: imagen.miniatura },
          });
          this.elegidaId.set(subida.id);
          this.cambiaron.emit();
        } catch (error: unknown) {
          const motivo = error instanceof ErrorImagen ? error.message : aErrorApi(error).mensaje;
          this.notificaciones.aviso(`No se agregó «${archivo.name}»: ${motivo}`);
        }
        this.subiendo.update((s) => (s ? { ...s, hechas: s.hechas + 1 } : s));
      }
    } finally {
      this.subiendo.set(null);
    }
  }
}
