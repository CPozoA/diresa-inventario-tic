import { ChangeDetectionStrategy, Component, computed, effect, inject, resource, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { catchError, debounceTime, distinctUntilChanged, firstValueFrom, from, map, of, switchMap } from 'rxjs';

import { Api } from '../../../api/api';
import { listarActivos } from '../../../api/fn/activos/listar-activos';
import { obtenerFichaActivo } from '../../../api/fn/activos/obtener-ficha-activo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { listarTecnicosMantenimiento } from '../../../api/fn/mantenimiento/listar-tecnicos-mantenimiento';
import { obtenerMantenimientosActivo } from '../../../api/fn/mantenimiento/obtener-mantenimientos-activo';
import { registrarMantenimiento } from '../../../api/fn/mantenimiento/registrar-mantenimiento';
import { ActivoResumenDto } from '../../../api/models/activo-resumen-dto';
import { EjecutorMantenimiento } from '../../../api/models/ejecutor-mantenimiento';
import { TipoMantenimiento } from '../../../api/models/tipo-mantenimiento';
import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { hoyIso } from '../../activos/data/formulario-activo';
import { formatoFecha } from '../../activos/data/textos-activo';
import { MESES_PREVENTIVO_POR_OMISION, sumarMeses } from '../data/mantenimiento';
import { CamposCierreComponent, cierreDesdeFormulario, crearFormularioCierre, noFutura } from './campos-cierre.component';
import { valorDe } from '../../../shared/util/recurso';

export interface DatosRegistrarMantenimiento {
  /** Activo; sin él, se busca en el diálogo. */
  readonly activoId?: string;
  /** Tipo propuesto (desde "Programados", preventivo). */
  readonly tipo?: NonNullable<TipoMantenimiento>;
}

/** Abre el diálogo; verdadero si se registró. */
export async function abrirRegistrarMantenimiento(dialogos: MatDialog, datos: DatosRegistrarMantenimiento): Promise<boolean> {
  const referencia = dialogos.open<RegistrarMantenimientoDialog, DatosRegistrarMantenimiento, boolean>(RegistrarMantenimientoDialog, {
    data: datos,
    width: '660px',
  });
  return (await firstValueFrom(referencia.afterClosed())) === true;
}

/**
 * Registrar un mantenimiento (D53): tipo, inicio, problema o actividades, y quién lo hace (personal de TI o proveedor).
 * Abierto, el activo pasa a "En mantenimiento"; con "Ya terminó" se registra cerrado, con los mismos datos del cierre.
 */
@Component({
  selector: 'app-registrar-mantenimiento-dialog',
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    AvisoComponent,
    CamposCierreComponent,
  ],
  templateUrl: './registrar-mantenimiento.dialog.html',
  styleUrl: './registrar-mantenimiento.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegistrarMantenimientoDialog {
  protected readonly valorDe = valorDe;
  protected readonly datos = inject<DatosRegistrarMantenimiento>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<RegistrarMantenimientoDialog, boolean>>(MatDialogRef);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly hoy = hoyIso();
  protected readonly formatoFecha = formatoFecha;
  protected readonly activoId = signal<string | null>(this.datos.activoId ?? null);
  protected readonly eligeActivo = !this.datos.activoId;

  protected readonly formulario = new FormGroup({
    tipo: new FormControl<NonNullable<TipoMantenimiento>>(this.datos.tipo ?? 'Correctivo', { nonNullable: true }),
    fechaInicio: new FormControl(this.hoy, { nonNullable: true, validators: [Validators.required, noFutura] }),
    detalle: new FormControl('', { nonNullable: true, validators: Validators.maxLength(1000) }),
    ejecutor: new FormControl<NonNullable<EjecutorMantenimiento>>('PersonalTi', { nonNullable: true }),
    tecnicoId: new FormControl<string | null>(null),
    proveedor: new FormControl('', { nonNullable: true, validators: Validators.maxLength(150) }),
    ordenServicio: new FormControl('', { nonNullable: true, validators: Validators.maxLength(50) }),
    costo: new FormControl<number | null>(null, Validators.min(0)),
    terminado: new FormControl(false, { nonNullable: true }),
  });

  protected readonly cierre = crearFormularioCierre(() => this.formulario.controls.fechaInicio.value, {
    conservacion: 'Bueno',
    proximo: null,
    costo: null,
  }, () => valorDe(this.delActivo)?.proximoMantenimiento ?? null);

  private readonly valor = toSignal(this.formulario.valueChanges, { initialValue: this.formulario.getRawValue() });
  protected readonly tipo = computed(() => this.valor().tipo ?? 'Correctivo');
  protected readonly ejecutor = computed(() => this.valor().ejecutor ?? 'PersonalTi');
  protected readonly terminado = computed(() => this.valor().terminado === true);

  // ---------- Activo ----------

  protected readonly ficha = resource({
    params: () => (this.activoId() ? { id: this.activoId()! } : undefined),
    loader: ({ params }) => this.api.invoke(obtenerFichaActivo, params),
  });

  protected readonly delActivo = resource({
    params: () => (this.activoId() ? { id: this.activoId()! } : undefined),
    loader: ({ params }) => this.api.invoke(obtenerMantenimientosActivo, params),
  });

  protected readonly titulo = computed(() => {
    const f = valorDe(this.ficha);
    if (!f) {
      return null;
    }
    const persona = f.asignacion ? ` · lo tiene ${f.asignacion.nombreCompleto}` : '';
    return `${f.generales.codigoPatrimonial} · ${f.generales.marca.nombre} ${f.generales.modelo}${persona}`;
  });

  /** Ya tiene uno en curso: no se registra otro. */
  protected readonly yaEnCurso = computed(() => valorDe(this.delActivo)?.enCurso ?? null);
  protected readonly deBaja = computed(() => valorDe(this.ficha)?.grupoEstado === 'DeBaja');

  protected readonly busquedaActivo = new FormControl<string | ActivoResumenDto>('', { nonNullable: true });
  protected readonly activosEncontrados = toSignal(
    this.busquedaActivo.valueChanges.pipe(
      map((v) => (typeof v === 'string' ? v.trim() : '')),
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((texto) =>
        texto === ''
          ? of<ActivoResumenDto[]>([])
          : from(
              this.api.invoke(listarActivos, {
                buscar: texto,
                tamanoPagina: 8,
                ...(this.sedeSeleccionada.sedeId() && this.sesion.esSuperAdmin() ? { sedeId: this.sedeSeleccionada.sedeId()! } : {}),
              }),
            ).pipe(
              map((p) => p.activos),
              catchError(() => of<ActivoResumenDto[]>([])),
            ),
      ),
    ),
    { initialValue: [] as ActivoResumenDto[] },
  );

  protected readonly mostrarActivo = (v: string | ActivoResumenDto | null): string =>
    typeof v === 'string' ? v : v ? v.codigoPatrimonial : '';

  // ---------- Técnicos y estados ----------

  protected readonly tecnicos = resource({
    params: () => {
      const f = valorDe(this.ficha);
      return f ? { sedeId: f.sedeId } : undefined;
    },
    loader: ({ params }) => this.api.invoke(listarTecnicosMantenimiento, params),
  });

  protected readonly estados = resource({
    loader: () => this.api.invoke(listarElementosCatalogo, { tipo: 'estados-activo', incluirInactivos: true }),
  });

  protected readonly intento = signal(false);
  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  constructor() {
    // El problema es obligatorio en un correctivo; el técnico o la empresa, según quién lo hace.
    this.formulario.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.ajustarValidadores());
    this.ajustarValidadores();

    // Al llegar la ficha: conservación y próximo preventivo actuales para el cierre.
    effect(() => {
      const f = valorDe(this.ficha);
      const m = valorDe(this.delActivo);
      untracked(() => {
        if (f) {
          this.cierre.controls.conservacion.setValue(f.generales.conservacion ?? 'Bueno');
        }
        if (m && this.tipo() !== 'Preventivo' && !this.cierre.controls.proximo.dirty) {
          this.cierre.controls.proximo.setValue(m.proximoMantenimiento ?? '');
        }
      });
    });

    // El técnico por omisión es quien registra, si está en la lista.
    effect(() => {
      const lista = valorDe(this.tecnicos) ?? [];
      untracked(() => {
        const yo = this.sesion.sesion()?.usuarioId;
        if (!this.formulario.controls.tecnicoId.value && lista.some((t) => t.id === yo)) {
          this.formulario.controls.tecnicoId.setValue(yo ?? null);
        }
      });
    });

    // Preventivo terminado: se propone el siguiente en 6 meses desde el término.
    effect(() => {
      const tipo = this.tipo();
      const terminado = this.terminado();
      untracked(() => {
        if (terminado && tipo === 'Preventivo' && !this.cierre.controls.proximo.dirty) {
          this.cierre.controls.proximo.setValue(sumarMeses(this.cierre.controls.fechaFin.value || hoyIso(), MESES_PREVENTIVO_POR_OMISION));
        }
      });
    });
  }

  protected elegirActivo(evento: MatAutocompleteSelectedEvent): void {
    const a = evento.option.value as ActivoResumenDto;
    this.activoId.set(a.id);
    this.busquedaActivo.setValue('', { emitEvent: false });
  }

  protected cambiarActivo(): void {
    this.activoId.set(null);
    this.formulario.controls.tecnicoId.setValue(null);
  }

  protected async guardar(): Promise<void> {
    this.intento.set(true);
    const f = valorDe(this.ficha);
    const terminado = this.terminado();
    if (!f || this.yaEnCurso() || this.deBaja() || this.formulario.invalid || (terminado && this.cierre.invalid) || this.guardando()) {
      this.formulario.markAllAsTouched();
      this.cierre.markAllAsTouched();
      return;
    }

    const v = this.formulario.getRawValue();
    const proveedor = v.ejecutor === 'Proveedor';
    this.guardando.set(true);
    this.error.set(null);
    try {
      await this.api.invoke(registrarMantenimiento, {
        id: f.id,
        body: {
          tipo: v.tipo,
          fechaInicio: v.fechaInicio,
          detalle: v.detalle.trim() || null,
          ejecutor: v.ejecutor,
          tecnicoId: proveedor ? null : v.tecnicoId,
          proveedor: proveedor ? v.proveedor.trim() : null,
          ordenServicio: proveedor ? v.ordenServicio.trim() || null : null,
          costo: proveedor && v.costo !== null && String(v.costo) !== '' ? v.costo : null,
          cierre: terminado ? cierreDesdeFormulario(this.cierre, proveedor, valorDe(this.delActivo)?.proximoMantenimiento ?? null) : null,
        },
      });
      this.referencia.close(true);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }

  private ajustarValidadores(): void {
    const c = this.formulario.controls;
    const v = this.formulario.getRawValue();
    const poner = (control: FormControl<unknown>, obligatorio: boolean) => {
      const tiene = control.hasValidator(Validators.required);
      if (obligatorio && !tiene) {
        control.addValidators(Validators.required);
      } else if (!obligatorio && tiene) {
        control.removeValidators(Validators.required);
      } else {
        return;
      }
      control.updateValueAndValidity({ emitEvent: false });
    };
    poner(c.detalle as FormControl<unknown>, v.tipo === 'Correctivo');
    poner(c.tecnicoId as FormControl<unknown>, v.ejecutor === 'PersonalTi');
    poner(c.proveedor as FormControl<unknown>, v.ejecutor === 'Proveedor');
  }
}

