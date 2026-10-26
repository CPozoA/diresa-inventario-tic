import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { EdicionFotos } from '../data/edicion-fotos';
import { FotoEnFormulario, LONGITUD_DESCRIPCION_FOTO, MAXIMO_FOTOS, formatoTamano, nombreFoto } from '../data/fotos';

/**
 * Sección "Fotos" del formulario del activo (D38, D47): tomar foto (en el celular), elegir o arrastrar archivos,
 * descripción, principal y quitar. Nada se envía hasta que se guarda el formulario.
 */
@Component({
  selector: 'app-fotos-formulario',
  imports: [MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatProgressBarModule],
  templateUrl: './fotos-formulario.component.html',
  styleUrl: './fotos-formulario.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FotosFormularioComponent {
  readonly edicion = input.required<EdicionFotos>();
  /** Mientras se guarda no se puede cambiar nada. */
  readonly bloqueado = input(false);

  protected readonly maximo = MAXIMO_FOTOS;
  protected readonly longitud = LONGITUD_DESCRIPCION_FOTO;
  protected readonly formatoTamano = formatoTamano;
  protected readonly nombreFoto = nombreFoto;
  protected readonly arrastrando = signal(false);
  /** Un lugar por cada imagen que se está reduciendo. */
  protected readonly enPreparacion = computed(() => Array.from({ length: this.edicion().preparando() }, (_, i) => i));

  /** "Tomar foto" solo tiene sentido en pantallas táctiles (celular o tableta); en la computadora abre los archivos. */
  protected readonly esTactil = typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;

  protected elegir(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivos = Array.from(entrada.files ?? []);
    // Se limpia para que elegir de nuevo el mismo archivo vuelva a avisar.
    entrada.value = '';
    void this.edicion().agregarArchivos(archivos);
  }

  protected alArrastrar(evento: DragEvent, encima: boolean): void {
    if (this.bloqueado() || !evento.dataTransfer?.types.includes('Files')) {
      return;
    }
    evento.preventDefault();
    this.arrastrando.set(encima);
  }

  protected alSoltar(evento: DragEvent): void {
    if (this.bloqueado()) {
      return;
    }
    evento.preventDefault();
    this.arrastrando.set(false);
    void this.edicion().agregarArchivos(Array.from(evento.dataTransfer?.files ?? []));
  }

  protected describir(foto: FotoEnFormulario, evento: Event): void {
    this.edicion().describir(foto.clave, (evento.target as HTMLInputElement).value);
  }

  protected detalle(foto: FotoEnFormulario): string {
    if (foto.imagen && !foto.id) {
      return `${formatoTamano(foto.imagen.tamanoOriginal)} → ${formatoTamano(foto.tamanoBytes)}`;
    }
    return `Guardada · ${formatoTamano(foto.tamanoBytes)}`;
  }

  protected estado(foto: FotoEnFormulario): string | null {
    switch (foto.estado) {
      case 'subiendo':
        return 'Subiendo…';
      case 'error':
        return 'No se subió';
      default:
        return null;
    }
  }
}
