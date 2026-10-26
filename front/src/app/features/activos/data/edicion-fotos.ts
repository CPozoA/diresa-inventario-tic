import { computed, signal } from '@angular/core';

import { Api } from '../../../api/api';
import { describirFotoActivo } from '../../../api/fn/fotos-de-activos/describir-foto-activo';
import { eliminarFotoActivo } from '../../../api/fn/fotos-de-activos/eliminar-foto-activo';
import { listarFotosActivo } from '../../../api/fn/fotos-de-activos/listar-fotos-activo';
import { marcarFotoPrincipalActivo } from '../../../api/fn/fotos-de-activos/marcar-foto-principal-activo';
import { subirFotoActivo } from '../../../api/fn/fotos-de-activos/subir-foto-activo';
import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import { aErrorApi } from '../../../core/http/error-api';
import {
  FotoEnFormulario,
  ImagenReducida,
  MAXIMO_FOTOS,
  agregarFotos,
  describirFoto,
  fotosParaFormulario,
  marcarPrincipal,
  planDeFotos,
  planVacio,
  quitarFoto,
  urlFoto,
} from './fotos';
import { ErrorImagen, reducirImagen } from './reducir-imagen';

/** Un archivo que no se agregó, con el motivo. */
export interface ArchivoRechazado {
  readonly nombre: string;
  readonly motivo: string;
}

/** Avance de la subida al guardar. */
export interface AvanceFotos {
  readonly hechas: number;
  readonly total: number;
}

/**
 * Fotos del formulario de un activo (D38, D47). Los cambios quedan en el navegador y se aplican al guardar:
 * si el usuario sale sin guardar, nada cambia. Las imágenes se reducen apenas se eligen.
 */
export class EdicionFotos {
  private contador = 0;
  private readonly guardadas = signal<FotoActivoDto[]>([]);

  readonly lista = signal<FotoEnFormulario[]>([]);
  readonly rechazadas = signal<ArchivoRechazado[]>([]);
  /** Cuántas imágenes se están reduciendo. */
  readonly preparando = signal(0);
  readonly avance = signal<AvanceFotos | null>(null);

  readonly lugares = computed(() => MAXIMO_FOTOS - this.lista().length - this.preparando());
  readonly hayCambios = computed(() => this.preparando() > 0 || !planVacio(planDeFotos(this.guardadas(), this.lista())));

  constructor(
    private readonly api: Api,
    private readonly raiz: string,
    /** Para probar sin un navegador real. */
    private readonly reducir: (archivo: File) => Promise<ImagenReducida> = reducirImagen,
  ) {}

  /** Punto de partida al editar: las fotos guardadas. */
  cargar(activoId: string, fotos: readonly FotoActivoDto[]): void {
    this.guardadas.set([...fotos]);
    this.lista.set(fotosParaFormulario(fotos, (f) => urlFoto(this.raiz, activoId, f.id, true)));
  }

  /** Reduce y agrega las imágenes elegidas; las que no son imágenes o no caben se informan aparte. */
  async agregarArchivos(archivos: readonly File[]): Promise<void> {
    const rechazadas: ArchivoRechazado[] = [];
    const caben = archivos.slice(0, Math.max(0, this.lugares()));
    for (const archivo of archivos.slice(caben.length)) {
      rechazadas.push({ nombre: archivo.name, motivo: `Ya hay ${MAXIMO_FOTOS} fotos, el máximo. Quite una para agregar otra.` });
    }

    this.preparando.update((n) => n + caben.length);
    try {
      for (const archivo of caben) {
        try {
          const imagen = await this.reducir(archivo);
          this.contador += 1;
          const nueva: FotoEnFormulario = {
            // Los ids de la API son GUID: con ":" una clave local nunca coincide con uno.
            clave: `local:${this.contador}`,
            id: null,
            descripcion: '',
            descripcionGuardada: '',
            principal: false,
            tamanoBytes: imagen.foto.size,
            imagen,
            vista: URL.createObjectURL(imagen.miniatura),
            estado: 'pendiente',
            error: null,
          };
          this.lista.update((lista) => agregarFotos(lista, [nueva]));
        } catch (error: unknown) {
          rechazadas.push({
            nombre: archivo.name,
            motivo: error instanceof ErrorImagen ? error.message : 'No se pudo abrir la imagen; puede estar dañada.',
          });
        } finally {
          this.preparando.update((n) => n - 1);
        }
      }
    } finally {
      if (rechazadas.length > 0) {
        this.rechazadas.update((lista) => [...lista, ...rechazadas]);
      }
    }
  }

  quitar(clave: string): void {
    const foto = this.lista().find((f) => f.clave === clave);
    if (foto?.imagen) {
      URL.revokeObjectURL(foto.vista);
    }
    this.lista.update((lista) => quitarFoto(lista, clave));
  }

  marcarPrincipal(clave: string): void {
    this.lista.update((lista) => marcarPrincipal(lista, clave));
  }

  describir(clave: string, descripcion: string): void {
    this.lista.update((lista) => describirFoto(lista, clave, descripcion));
  }

  descartarRechazo(indice: number): void {
    this.rechazadas.update((lista) => lista.filter((_, i) => i !== indice));
  }

  /**
   * Aplica los cambios en la API, después de guardar la ficha: elimina, cambia descripciones, sube las nuevas una por
   * una y fija la principal. Si algo falla sigue con lo demás y devuelve el primer motivo; lo que quedó pendiente se
   * reintenta al volver a guardar.
   */
  async aplicar(activoId: string): Promise<string | null> {
    const plan = planDeFotos(this.guardadas(), this.lista());
    if (planVacio(plan)) {
      return null;
    }

    let primerError: string | null = null;
    const anotar = (error: unknown): string => {
      const mensaje = aErrorApi(error).mensaje;
      primerError ??= mensaje;
      return mensaje;
    };

    for (const fotoId of plan.eliminar) {
      try {
        await this.api.invoke(eliminarFotoActivo, { id: activoId, fotoId });
      } catch (error: unknown) {
        anotar(error);
      }
    }

    for (const cambio of plan.describir) {
      try {
        await this.api.invoke(describirFotoActivo, { id: activoId, fotoId: cambio.id, body: { descripcion: cambio.descripcion } });
      } catch (error: unknown) {
        anotar(error);
      }
    }

    this.avance.set({ hechas: 0, total: plan.subir.length });
    for (const clave of plan.subir) {
      const foto = this.lista().find((f) => f.clave === clave);
      if (!foto?.imagen) {
        continue;
      }
      this.cambiar(clave, { estado: 'subiendo', error: null });
      try {
        const subida = await this.api.invoke(subirFotoActivo, {
          id: activoId,
          // En el formulario multipart la descripción es opcional: sin texto, simplemente no se envía.
          body: { archivo: foto.imagen.foto, miniatura: foto.imagen.miniatura, descripcion: foto.descripcion.trim() || undefined },
        });
        this.cambiar(clave, { id: subida.id, descripcionGuardada: subida.descripcion ?? '', estado: null });
      } catch (error: unknown) {
        this.cambiar(clave, { estado: 'error', error: anotar(error) });
      }
      this.avance.update((a) => (a ? { ...a, hechas: a.hechas + 1 } : a));
    }
    this.avance.set(null);

    const principal = plan.principal ? this.lista().find((f) => f.clave === plan.principal) : null;
    if (principal?.id) {
      try {
        await this.api.invoke(marcarFotoPrincipalActivo, { id: activoId, fotoId: principal.id });
      } catch (error: unknown) {
        anotar(error);
      }
    }

    await this.releerGuardadas(activoId);
    return primerError;
  }

  /** Libera las imágenes nuevas que quedaron en memoria (al salir de la pantalla). */
  liberar(): void {
    for (const foto of this.lista()) {
      if (foto.imagen) {
        URL.revokeObjectURL(foto.vista);
      }
    }
  }

  private cambiar(clave: string, cambios: Partial<FotoEnFormulario>): void {
    this.lista.update((lista) => lista.map((f) => (f.clave === clave ? { ...f, ...cambios } : f)));
  }

  /** Lo que quedó en la API, para que el próximo intento solo haga lo que falta. */
  private async releerGuardadas(activoId: string): Promise<void> {
    try {
      const guardadas = await this.api.invoke(listarFotosActivo, { id: activoId });
      this.guardadas.set(guardadas);
      const porId = new Map(guardadas.map((f) => [f.id, f]));
      this.lista.update((lista) =>
        lista
          .filter((f) => !f.id || porId.has(f.id))
          .map((f) => (f.id ? { ...f, descripcionGuardada: porId.get(f.id)?.descripcion ?? '' } : f)),
      );
    } catch {
      // Sin la lista actualizada, el próximo intento puede repetir algún paso; la API lo tolera.
    }
  }
}
