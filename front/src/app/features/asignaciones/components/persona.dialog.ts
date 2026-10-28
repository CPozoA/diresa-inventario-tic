import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { listarEstablecimientos } from '../../../api/fn/establecimientos/listar-establecimientos';
import { consultarDni } from '../../../api/fn/personal/consultar-dni';
import { editarPersona } from '../../../api/fn/personal/editar-persona';
import { registrarPersona } from '../../../api/fn/personal/registrar-persona';
import { ConsultaDniDto } from '../../../api/models/consulta-dni-dto';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { PersonaDto } from '../../../api/models/persona-dto';
import { Permisos } from '../../../core/auth/permisos';
import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import {
  BuscadorEstablecimientoComponent,
  EstablecimientoElegido,
  detalleDeEstablecimiento,
} from '../../activos/components/buscador-establecimiento.component';
import { SelectorCatalogoComponent } from '../../catalogos/components/selector-catalogo.component';
import { EstadoDni, enMayusculas, esDniValido, estadoDesdeConsulta, limpiarDni, mensajeConsulta, seEscribenLosNombres } from '../data/personal';

export interface DatosPersonaDialog {
  /** Nula para registrar a una persona nueva. */
  readonly persona: PersonaDto | null;
  /** Red donde se registra: la elegida en el encabezado (superadministrador). Nula: la del establecimiento. */
  readonly sedeId: string | null;
  /** DNI ya escrito en otra pantalla (por ejemplo, al buscar a quién asignar un activo). */
  readonly dni?: string;
}

/** La persona registrada o editada. */
export interface ResultadoPersonaDialog {
  readonly id: string;
}

/** Abre el diálogo y devuelve la persona guardada, o nulo si se canceló. */
export async function abrirPersonaDialog(dialogos: MatDialog, datos: DatosPersonaDialog): Promise<ResultadoPersonaDialog | null> {
  const referencia = dialogos.open<PersonaDialog, DatosPersonaDialog, ResultadoPersonaDialog>(PersonaDialog, {
    data: datos,
    width: '600px',
    autoFocus: datos.persona ? 'dialog' : 'first-tabbable',
  });
  return (await firstValueFrom(referencia.afterClosed())) ?? null;
}

/**
 * Registrar o editar a una persona del personal de la red (D50). Al registrar se busca primero el DNI: si ya está en
 * la red, se avisa; si RENIEC lo encuentra, sus nombres no se escriben; si no lo encuentra o no responde, se escriben a
 * mano y la persona queda "sin verificar". Se usa desde Asignaciones, la ficha de la persona y el formulario del activo.
 */
@Component({
  selector: 'app-persona-dialog',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    AvisoComponent,
    BuscadorEstablecimientoComponent,
    SelectorCatalogoComponent,
  ],
  templateUrl: './persona.dialog.html',
  styleUrl: './persona.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonaDialog {
  protected readonly datos = inject<DatosPersonaDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<PersonaDialog, ResultadoPersonaDialog>>(MatDialogRef);
  private readonly sesion = inject(SesionStore);

  protected readonly persona = this.datos.persona;
  protected readonly creando = this.persona === null;
  protected readonly puedeAgregarAreas = this.sesion.tienePermiso(Permisos.Catalogos.GestionarSede);

  protected readonly formulario = new FormGroup({
    dni: new FormControl(limpiarDni(this.datos.dni ?? this.persona?.dni ?? ''), {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{8}$/)],
    }),
    apellidoPaterno: new FormControl(this.persona?.apellidoPaterno ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(60)] }),
    apellidoMaterno: new FormControl(this.persona?.apellidoMaterno ?? '', { nonNullable: true, validators: Validators.maxLength(60) }),
    nombres: new FormControl(this.persona?.nombres ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    cargo: new FormControl(this.persona?.cargo ?? '', { nonNullable: true, validators: Validators.maxLength(100) }),
    areaId: new FormControl<string | null>(this.persona?.areaId ?? null, Validators.required),
  });

  /** Búsqueda del DNI (solo al registrar). */
  protected readonly estado = signal<EstadoDni>('pendiente');
  protected readonly consulta = signal<ConsultaDniDto | null>(null);

  protected readonly establecimiento = signal<EstablecimientoElegido | null>(null);
  protected readonly intentoGuardar = signal(false);
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  /** Red donde se busca el establecimiento: la de la persona al editar, o la elegida en el encabezado. */
  protected readonly sedeId = this.persona?.sedeId ?? this.datos.sedeId;

  /** Los nombres se escriben a mano: al registrar, si RENIEC no los dio; al editar, si no está verificada. */
  protected readonly nombresAMano = computed(() => (this.creando ? seEscribenLosNombres(this.estado()) : !this.persona?.verificada));

  protected readonly mensajeConsulta = computed(() => mensajeConsulta(this.estado()));

  /** Áreas de la red del establecimiento elegido. */
  protected readonly areas = resource({
    params: () => {
      const e = this.establecimiento();
      return e ? { sedeId: e.sedeId } : undefined;
    },
    loader: ({ params }) => this.api.invoke(listarElementosCatalogo, { tipo: 'areas', sedeId: params.sedeId, incluirInactivos: true }),
  });

  /** Se ofrecen las activas y la que ya tenía (aunque esté desactivada). */
  protected readonly opcionesArea = computed(() =>
    (this.areas.value() ?? []).filter((a) => a.activo || a.id === this.persona?.areaId),
  );

  constructor() {
    // Los campos de nombres solo cuentan (y se validan) cuando se escriben a mano.
    effect(() => {
      const aMano = this.nombresAMano();
      untracked(() => {
        for (const control of [this.formulario.controls.apellidoPaterno, this.formulario.controls.apellidoMaterno, this.formulario.controls.nombres]) {
          if (aMano) {
            control.enable({ emitEvent: false });
          } else {
            control.disable({ emitEvent: false });
          }
        }
      });
    });

    // Si el establecimiento cambia a otra red, el área elegida ya no corresponde.
    effect(() => {
      const areas = this.areas.value();
      untracked(() => {
        const elegida = this.formulario.controls.areaId.value;
        if (areas && elegida && !areas.some((a) => a.id === elegida)) {
          this.formulario.controls.areaId.setValue(null);
        }
      });
    });

    // Cambiar el DNI invalida la búsqueda anterior.
    this.formulario.controls.dni.valueChanges.pipe(takeUntilDestroyed()).subscribe((valor) => {
      const limpio = limpiarDni(valor);
      if (limpio !== valor) {
        this.formulario.controls.dni.setValue(limpio, { emitEvent: false });
      }
      if (this.estado() !== 'pendiente' && limpio !== this.consulta()?.dni) {
        this.estado.set('pendiente');
        this.consulta.set(null);
      }
    });

    if (this.persona) {
      void this.cargarEstablecimiento(this.persona);
    } else if (esDniValido(this.formulario.controls.dni.value)) {
      void this.buscarDni();
    }
  }

  protected sumarArea(area: ElementoCatalogoDto): void {
    this.areas.update((lista) => [...(lista ?? []), area]);
  }

  protected async buscarDni(evento?: Event): Promise<void> {
    evento?.preventDefault();
    const dni = this.formulario.controls.dni.value;
    if (!esDniValido(dni) || this.estado() === 'consultando') {
      this.formulario.controls.dni.markAsTouched();
      return;
    }

    this.estado.set('consultando');
    this.error.set(null);
    try {
      const sedeId = this.datos.sedeId ?? this.establecimiento()?.sedeId ?? null;
      const consulta = await this.api.invoke(consultarDni, { body: { dni, sedeId } });
      this.consulta.set(consulta);
      this.estado.set(estadoDesdeConsulta(consulta));
    } catch (error: unknown) {
      this.estado.set('pendiente');
      this.error.set(aErrorApi(error));
    }
  }

  protected async guardar(): Promise<void> {
    if (this.guardando()) {
      return;
    }

    // Al registrar, primero se busca el DNI: así se ven los nombres de RENIEC antes de guardar.
    if (this.creando && this.estado() === 'pendiente') {
      await this.buscarDni();
      return;
    }
    if (this.estado() === 'consultando' || this.estado() === 'registrada') {
      return;
    }

    this.intentoGuardar.set(true);
    const establecimiento = this.establecimiento();
    if (this.formulario.invalid || !establecimiento) {
      this.formulario.markAllAsTouched();
      return;
    }

    const v = this.formulario.getRawValue();
    const aMano = this.nombresAMano();
    const nombres = {
      apellidoPaterno: aMano ? enMayusculas(v.apellidoPaterno) : null,
      apellidoMaterno: aMano && v.apellidoMaterno.trim() !== '' ? enMayusculas(v.apellidoMaterno) : null,
      nombres: aMano ? enMayusculas(v.nombres) : null,
    };
    const cargo = v.cargo.trim() === '' ? null : v.cargo.trim();

    this.guardando.set(true);
    this.error.set(null);
    try {
      if (this.persona) {
        await this.api.invoke(editarPersona, {
          id: this.persona.id,
          body: { establecimientoId: establecimiento.id, areaId: v.areaId!, cargo, ...nombres },
        });
        this.referencia.close({ id: this.persona.id });
      } else {
        const creado = await this.api.invoke(registrarPersona, {
          body: { dni: v.dni, establecimientoId: establecimiento.id, areaId: v.areaId!, cargo, ...nombres },
        });
        this.referencia.close({ id: creado.id });
      }
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }

  /** Al editar, el establecimiento actual con su código y detalle (para el buscador). */
  private async cargarEstablecimiento(persona: PersonaDto): Promise<void> {
    try {
      const lista = await this.api.invoke(listarEstablecimientos, { sedeId: persona.sedeId, incluirInactivos: true });
      const e = lista.find((x) => x.id === persona.establecimientoId);
      this.establecimiento.set(
        e
          ? { id: e.id, sedeId: e.sedeId, codigoUnico: e.codigoUnico, nombre: e.nombre, detalle: detalleDeEstablecimiento(e, false) }
          : { id: persona.establecimientoId, sedeId: persona.sedeId, codigoUnico: '—', nombre: persona.establecimiento, detalle: '' },
      );
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    }
  }
}
