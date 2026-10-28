import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { ApiConfiguration } from '../../../api/api-configuration';
import { cesarPersona } from '../../../api/fn/personal/cesar-persona';
import { obtenerPersona } from '../../../api/fn/personal/obtener-persona';
import { reactivarPersona } from '../../../api/fn/personal/reactivar-persona';
import { verificarPersona } from '../../../api/fn/personal/verificar-persona';
import { EquipoPersonaDto } from '../../../api/models/equipo-persona-dto';
import { PersonaDto } from '../../../api/models/persona-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SiTienePermisoDirective } from '../../../core/auth/si-tiene-permiso.directive';
import { aErrorApi } from '../../../core/http/error-api';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { rutaInternaSegura } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { urlFoto } from '../../activos/data/fotos';
import { formatoFecha, textoConservacion } from '../../activos/data/textos-activo';
import { abrirPersonaDialog } from '../components/persona.dialog';
import { conservacionHistorial, iniciales, textoEquipos } from '../data/personal';

const LISTA = '/asignaciones';

/**
 * Ficha de una persona del personal (D50): sus datos, los equipos que tiene hoy y su historial. Con Activos.Editar se
 * edita, se verifica con RENIEC y se marca como cesada (antes debe devolver sus equipos) o se reactiva.
 */
@Component({
  selector: 'app-ficha-persona',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, AvisoComponent, SiTienePermisoDirective],
  templateUrl: './ficha-persona.page.html',
  styleUrl: './ficha-persona.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FichaPersonaPage {
  /** Identificador de la ruta (personal/:id). */
  readonly id = input.required<string>();

  private readonly api = inject(Api);
  private readonly raiz = inject(ApiConfiguration).rootUrl;
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly router = inject(Router);
  private readonly ubicacion = inject(Location);
  private readonly sesion = inject(SesionStore);

  protected readonly permisos = Permisos;
  protected readonly iniciales = iniciales;
  protected readonly formatoFecha = formatoFecha;
  protected readonly textoConservacion = textoConservacion;
  protected readonly conservacionHistorial = conservacionHistorial;
  protected readonly esSuperAdmin = this.sesion.esSuperAdmin;

  protected readonly recurso = resource({
    params: () => ({ id: this.id() }),
    loader: ({ params }) => this.api.invoke(obtenerPersona, params),
  });

  protected readonly persona = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
  protected readonly error = computed(() => (this.recurso.error() ? aErrorApi(this.recurso.error()).mensaje : null));

  /** Equipos que tiene hoy (asignaciones sin devolver). */
  protected readonly aCargo = computed(() => (this.persona()?.equipos ?? []).filter((e) => !e.fechaDevolucion));
  protected readonly textoACargo = computed(() => textoEquipos(this.aCargo().length));

  /** "Asistente administrativo · Hospital de Huanta · Administración". */
  protected readonly lugar = computed(() => {
    const p = this.persona();
    return p ? [p.cargo, p.establecimiento, p.area, this.esSuperAdmin() ? p.sedeNombre : null].filter((x) => !!x).join(' · ') : '';
  });

  /** Aviso al intentar marcarla como cesada mientras tiene equipos. */
  protected readonly avisoCese = signal(false);
  protected readonly trabajando = signal(false);

  protected miniatura(e: EquipoPersonaDto): string | null {
    return e.fotoPrincipalId ? urlFoto(this.raiz, e.activoId, e.fotoPrincipalId, true) : null;
  }

  protected equipo(e: EquipoPersonaDto): string {
    return `${e.marca} ${e.modelo} · ${e.tipoActivo}`;
  }

  protected async editar(p: PersonaDto): Promise<void> {
    if (await abrirPersonaDialog(this.dialogos, { persona: p, sedeId: p.sedeId })) {
      this.notificaciones.exito('Datos guardados.');
      this.recurso.reload();
    }
  }

  protected async verificar(p: PersonaDto): Promise<void> {
    await this.ejecutar(() => this.api.invoke(verificarPersona, { id: p.id }), 'Nombres confirmados con RENIEC.');
  }

  protected async cesar(p: PersonaDto): Promise<void> {
    if (this.aCargo().length > 0) {
      this.avisoCese.set(true);
      return;
    }
    const confirmado = await confirmar(this.dialogos, {
      titulo: `Marcar como cesada a ${p.nombreCompleto}`,
      mensaje: 'Ya no aparecerá al asignar equipos. Su historial se conserva y se puede reactivar después.',
      confirmar: 'Marcar como cesada',
      peligro: true,
    });
    if (confirmado) {
      await this.ejecutar(() => this.api.invoke(cesarPersona, { id: p.id }), 'Persona marcada como cesada.');
    }
  }

  protected async reactivar(p: PersonaDto): Promise<void> {
    await this.ejecutar(() => this.api.invoke(reactivarPersona, { id: p.id }), 'Persona reactivada.');
  }

  /** Vuelve a la lista con los mismos filtros con que se abrió la ficha (si se llegó desde ella). */
  protected volver(evento: Event): void {
    evento.preventDefault();
    void this.router.navigateByUrl(this.urlDeLista());
  }

  protected urlDeLista(): string {
    const estado = this.ubicacion.getState() as { volver?: unknown } | null;
    const volver = typeof estado?.volver === 'string' ? rutaInternaSegura(estado.volver) : LISTA;
    return volver === LISTA || volver.startsWith(`${LISTA}?`) ? volver : LISTA;
  }

  private async ejecutar(accion: () => Promise<unknown>, exito: string): Promise<void> {
    if (this.trabajando()) {
      return;
    }
    this.trabajando.set(true);
    this.avisoCese.set(false);
    try {
      await accion();
      this.notificaciones.exito(exito);
      this.recurso.reload();
    } catch (error: unknown) {
      this.notificaciones.error(error);
    } finally {
      this.trabajando.set(false);
    }
  }
}
