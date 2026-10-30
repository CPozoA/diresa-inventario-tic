import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';

import { Api } from '../../../api/api';
import { actualizarLicencia } from '../../../api/fn/licencias/actualizar-licencia';
import { listarProductosLicencia } from '../../../api/fn/licencias/listar-productos-licencia';
import { registrarLicencia } from '../../../api/fn/licencias/registrar-licencia';
import { FichaLicenciaDto } from '../../../api/models/ficha-licencia-dto';
import { LicenciaRequest } from '../../../api/models/licencia-request';
import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { valorDe } from '../../../shared/util/recurso';
import { hoyIso } from '../../activos/data/formulario-activo';
import { aNumero, formatoFecha } from '../../activos/data/textos-activo';
import { sumarMeses } from '../../mantenimiento/data/mantenimiento';
import { periodoRenovacion, sumarDias, unAnoDesde } from '../data/licencias';

export interface DatosLicenciaDialog {
  /** Nula para registrar una licencia nueva. */
  readonly ficha: FichaLicenciaDto | null;
  /** Con la ficha: propone el periodo siguiente (nuevas fechas, sin documento ni costo). */
  readonly renovar?: boolean;
}

/** Abre el diálogo. Devuelve el id de la licencia guardada, o nulo si se canceló. */
export async function abrirLicenciaDialog(dialogos: MatDialog, datos: DatosLicenciaDialog): Promise<string | null> {
  const referencia = dialogos.open<LicenciaDialog, DatosLicenciaDialog, string>(LicenciaDialog, {
    data: datos,
    width: '640px',
    autoFocus: datos.ficha ? 'dialog' : 'first-tabbable',
  });
  return (await firstValueFrom(referencia.afterClosed())) ?? null;
}

/** El vencimiento debe ser posterior al inicio. */
function finPosterior(grupo: AbstractControl): ValidationErrors | null {
  const inicio = grupo.get('fechaInicio')?.value as string;
  const fin = grupo.get('fechaFin')?.value as string;
  return inicio && fin && fin <= inicio ? { finAnterior: true } : null;
}

/**
 * Registrar, editar o renovar una suscripción de software (D54). No se guarda la clave del producto. El
 * superadministrador con "Todas las redes" elige la red.
 */
@Component({
  selector: 'app-licencia-dialog',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, AvisoComponent],
  templateUrl: './licencia.dialog.html',
  styleUrl: './licencia.dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenciaDialog {
  protected readonly datos = inject<DatosLicenciaDialog>(MAT_DIALOG_DATA);
  private readonly api = inject(Api);
  private readonly referencia = inject<MatDialogRef<LicenciaDialog, string>>(MatDialogRef);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);

  protected readonly formatoFecha = formatoFecha;
  protected readonly ficha = this.datos.ficha;
  protected readonly renovar = !!this.datos.ficha && this.datos.renovar === true;
  protected readonly titulo = !this.ficha ? 'Nueva licencia' : this.renovar ? 'Renovar licencia' : 'Editar licencia';
  protected readonly atajos = [
    { meses: 12, texto: '1 año' },
    { meses: 24, texto: '2 años' },
    { meses: 36, texto: '3 años' },
  ];

  /** El superadministrador sin red elegida arriba debe indicarla al registrar. */
  protected readonly eligeRed = !this.ficha && this.sesion.esSuperAdmin() && this.sedeSeleccionada.sedeId() === null;
  protected readonly sedes = this.sedeSeleccionada.sedes;

  protected readonly formulario = this.crearFormulario();

  protected readonly productos = resource({
    loader: () => this.api.invoke(listarProductosLicencia),
  });
  private readonly textoProducto = toSignal(this.formulario.controls.producto.valueChanges, {
    initialValue: this.formulario.controls.producto.value,
  });
  protected readonly sugerencias = computed(() => {
    const texto = this.textoProducto().trim().toLowerCase();
    const todos = valorDe(this.productos) ?? [];
    return todos.filter((p) => p.toLowerCase().includes(texto) && p.toLowerCase() !== texto).slice(0, 8);
  });

  private readonly puestos = toSignal(this.formulario.controls.puestos.valueChanges, { initialValue: this.formulario.controls.puestos.value });
  /** Equipos que quedarían sin puesto al bajar la cantidad. */
  protected readonly deMas = computed(() => {
    const equipos = this.ficha?.equipos.length ?? 0;
    const puestos = aNumero(this.puestos()) ?? 0;
    return puestos > 0 && equipos > puestos ? equipos - puestos : 0;
  });

  protected readonly guardando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected programar(meses: number): void {
    const c = this.formulario.controls;
    const inicio = c.fechaInicio.value || hoyIso();
    c.fechaFin.setValue(sumarDias(sumarMeses(inicio, meses), -1));
    c.fechaFin.markAsTouched();
  }

  protected async guardar(): Promise<void> {
    if (this.formulario.invalid || this.guardando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    const v = this.formulario.getRawValue();
    const cuerpo: LicenciaRequest = {
      sedeId: this.eligeRed ? v.sedeId : (this.sedeSeleccionada.sedeId() ?? null),
      producto: v.producto.trim(),
      proveedor: v.proveedor.trim() || null,
      puestos: v.puestos ?? 0,
      fechaInicio: v.fechaInicio,
      fechaFin: v.fechaFin,
      documentoCompra: v.documentoCompra.trim() || null,
      costo: v.costo !== null && String(v.costo) !== '' ? v.costo : null,
      observaciones: v.observaciones.trim() || null,
    };

    this.guardando.set(true);
    this.error.set(null);
    try {
      if (this.ficha) {
        await this.api.invoke(actualizarLicencia, { id: this.ficha.id, body: { ...cuerpo, sedeId: null } });
        this.referencia.close(this.ficha.id);
      } else {
        const creada = await this.api.invoke(registrarLicencia, { body: cuerpo });
        this.referencia.close(creada.id);
      }
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.guardando.set(false);
    }
  }

  private crearFormulario() {
    const f = this.ficha;
    const periodo = f && this.renovar ? periodoRenovacion(f.fechaFin) : null;
    const inicio = periodo?.inicio ?? f?.fechaInicio ?? hoyIso();
    return new FormGroup(
      {
        sedeId: new FormControl<string | null>(null, this.eligeRed ? Validators.required : []),
        producto: new FormControl(f?.producto ?? '', { nonNullable: true, validators: [Validators.required, Validators.maxLength(150)] }),
        proveedor: new FormControl(f?.proveedor ?? '', { nonNullable: true, validators: Validators.maxLength(150) }),
        puestos: new FormControl<number | null>(f ? aNumero(f.puestos) : 1, [Validators.required, Validators.min(1), Validators.max(100000), Validators.pattern(/^\d+$/)]),
        fechaInicio: new FormControl(inicio, { nonNullable: true, validators: Validators.required }),
        fechaFin: new FormControl(periodo?.fin ?? f?.fechaFin ?? unAnoDesde(inicio), { nonNullable: true, validators: Validators.required }),
        documentoCompra: new FormControl(this.renovar ? '' : (f?.documentoCompra ?? ''), { nonNullable: true, validators: Validators.maxLength(50) }),
        costo: new FormControl<number | null>(this.renovar ? null : aNumero(f?.costo), Validators.min(0)),
        observaciones: new FormControl(f?.observaciones ?? '', { nonNullable: true, validators: Validators.maxLength(500) }),
      },
      { validators: finPosterior },
    );
  }
}
