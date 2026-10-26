import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { Api } from '../../../api/api';
import { ApiConfiguration } from '../../../api/api-configuration';
import { describirFotoActivo } from '../../../api/fn/fotos-de-activos/describir-foto-activo';
import { eliminarFotoActivo } from '../../../api/fn/fotos-de-activos/eliminar-foto-activo';
import { marcarFotoPrincipalActivo } from '../../../api/fn/fotos-de-activos/marcar-foto-principal-activo';
import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { LONGITUD_DESCRIPCION_FOTO, formatoTamano, nombreFoto, urlFoto } from '../data/fotos';
import { aNumero, formatoFecha } from '../data/textos-activo';

export interface DatosVisorFotos {
  readonly activoId: string;
  /** "PAT-0001 · Dell Latitude 5440". */
  readonly titulo: string;
  readonly fotos: readonly FotoActivoDto[];
  readonly indice: number;
  readonly puedeEditar: boolean;
  /** Se llama después de cada cambio guardado, para que la ficha vuelva a pedir las fotos. */
  readonly alCambiar: () => void;
}

/** Abre el visor a pantalla completa. */
export function abrirVisorFotos(dialogos: MatDialog, datos: DatosVisorFotos): void {
  dialogos.open(VisorFotosDialog, {
    data: datos,
    panelClass: 'visor-fotos-panel',
    width: '100vw',
    height: '100dvh',
    maxWidth: '100vw',
    maxHeight: '100dvh',
    autoFocus: 'dialog',
  });
}

/**
 * Visor de las fotos de un activo (D38): una a una, con flechas o el teclado. Quien puede editar marca la principal,
 * cambia la descripción o elimina; cada cambio se guarda al momento.
 */
@Component({
  selector: 'app-visor-fotos',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './visor-fotos.dialog.html',
  styleUrl: './visor-fotos.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // En la ventana: el foco suele estar en el contenedor del diálogo, fuera de este componente.
  host: { '(window:keydown)': 'alTeclear($event)' },
})
export class VisorFotosDialog {
  private readonly api = inject(Api);
  private readonly raiz = inject(ApiConfiguration).rootUrl;
  private readonly dialogos = inject(MatDialog);
  private readonly referencia = inject(MatDialogRef<VisorFotosDialog>);
  private readonly notificaciones = inject(NotificacionesService);
  protected readonly datos = inject<DatosVisorFotos>(MAT_DIALOG_DATA);

  protected readonly longitud = LONGITUD_DESCRIPCION_FOTO;
  protected readonly nombreFoto = nombreFoto;

  protected readonly fotos = signal<readonly FotoActivoDto[]>(this.datos.fotos);
  protected readonly indice = signal(this.datos.indice);
  protected readonly ocupado = signal(false);

  protected readonly actual = computed(() => this.fotos()[Math.min(this.indice(), this.fotos().length - 1)] ?? null);
  protected readonly detalle = computed(() => {
    const f = this.actual();
    return f ? `Subida el ${formatoFecha(f.creadoEn)} · ${formatoTamano(aNumero(f.tamanoBytes) ?? 0)}` : '';
  });

  protected url(foto: FotoActivoDto, miniatura: boolean): string {
    return urlFoto(this.raiz, this.datos.activoId, foto.id, miniatura);
  }

  protected anterior(): void {
    const total = this.fotos().length;
    this.indice.update((i) => (i - 1 + total) % total);
  }

  protected siguiente(): void {
    this.indice.update((i) => (i + 1) % this.fotos().length);
  }

  protected alTeclear(evento: KeyboardEvent): void {
    // Ni mientras se escribe la descripción, ni si hay otro diálogo encima (la confirmación de eliminar).
    const arriba = this.dialogos.openDialogs.at(-1) === this.referencia;
    if (!arriba || evento.target instanceof HTMLInputElement || this.fotos().length < 2) {
      return;
    }
    if (evento.key === 'ArrowLeft') {
      evento.preventDefault();
      this.anterior();
    } else if (evento.key === 'ArrowRight') {
      evento.preventDefault();
      this.siguiente();
    }
  }

  protected async marcarPrincipal(): Promise<void> {
    const foto = this.actual();
    if (!foto || foto.principal) {
      return;
    }
    await this.ejecutar(async () => {
      await this.api.invoke(marcarFotoPrincipalActivo, { id: this.datos.activoId, fotoId: foto.id });
      this.fotos.update((lista) => lista.map((f) => ({ ...f, principal: f.id === foto.id })));
      this.notificaciones.exito('Es la nueva foto principal.');
    });
  }

  /** Se guarda al salir del campo o con Enter, solo si cambió. */
  protected async describir(evento: Event): Promise<void> {
    const foto = this.actual();
    const entrada = evento.target as HTMLInputElement;
    const descripcion = entrada.value.trim();
    if (!foto || descripcion === (foto.descripcion ?? '')) {
      return;
    }
    await this.ejecutar(async () => {
      await this.api.invoke(describirFotoActivo, {
        id: this.datos.activoId,
        fotoId: foto.id,
        body: { descripcion: descripcion || null },
      });
      this.fotos.update((lista) => lista.map((f) => (f.id === foto.id ? { ...f, descripcion: descripcion || null } : f)));
      this.notificaciones.exito('Descripción guardada.');
    });
  }

  protected async eliminar(): Promise<void> {
    const foto = this.actual();
    if (!foto) {
      return;
    }
    const restantes = this.fotos().filter((f) => f.id !== foto.id);
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Eliminar «${nombreFoto(foto.descripcion, this.indice() + 1)}»`,
      mensaje:
        foto.principal && restantes.length > 0
          ? 'Es la foto principal: la siguiente pasará a serlo. Se borra del servidor y no se puede deshacer.'
          : 'Se borra del servidor y no se puede deshacer.',
      confirmar: 'Eliminar',
      peligro: true,
    });
    if (!confirmado) {
      return;
    }
    await this.ejecutar(async () => {
      await this.api.invoke(eliminarFotoActivo, { id: this.datos.activoId, fotoId: foto.id });
      if (restantes.length === 0) {
        this.referencia.close();
        return;
      }
      const conPrincipal = restantes.some((f) => f.principal)
        ? restantes
        : restantes.map((f, i) => (i === 0 ? { ...f, principal: true } : f));
      this.fotos.set(conPrincipal);
      this.indice.update((i) => Math.min(i, conPrincipal.length - 1));
      this.notificaciones.exito('Foto eliminada.');
    });
  }

  private async ejecutar(accion: () => Promise<void>): Promise<void> {
    this.ocupado.set(true);
    try {
      await accion();
      this.datos.alCambiar();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.ocupado.set(false);
    }
  }
}
