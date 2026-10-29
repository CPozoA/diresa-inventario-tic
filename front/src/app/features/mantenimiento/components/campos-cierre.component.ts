import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, untracked } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { CierreMantenimiento } from '../../../api/models/cierre-mantenimiento';
import { Conservacion } from '../../../api/models/conservacion';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { ResultadoMantenimiento } from '../../../api/models/resultado-mantenimiento';
import { TipoMantenimiento } from '../../../api/models/tipo-mantenimiento';
import { hoyIso } from '../../activos/data/formulario-activo';
import { TEXTO_CONSERVACION } from '../../activos/data/textos-activo';
import { estadoPropuesto, sumarMeses } from '../data/mantenimiento';

/** Fecha "aaaa-mm-dd" no anterior a otra que puede cambiar (el inicio del mantenimiento). */
export function noAntesDe(minima: () => string | null): ValidatorFn {
  return (c: AbstractControl): ValidationErrors | null => {
    const m = minima();
    return m && c.value && String(c.value) < m ? { anterior: true } : null;
  };
}

/** Fecha no posterior a hoy. */
export const noFutura: ValidatorFn = (c) => (c.value && String(c.value) > hoyIso() ? { futura: true } : null);

export type FormularioCierre = FormGroup<{
  fechaFin: FormControl<string>;
  trabajo: FormControl<string>;
  resultado: FormControl<NonNullable<ResultadoMantenimiento>>;
  costo: FormControl<number | null>;
  estadoActivoId: FormControl<string | null>;
  conservacion: FormControl<NonNullable<Conservacion>>;
  proximo: FormControl<string>;
}>;

/**
 * Campos del cierre de un mantenimiento (D53). `inicio` devuelve la fecha de inicio (el término no puede ser
 * anterior) y `proximoActual`, el próximo preventivo que ya tiene el activo.
 */
export function crearFormularioCierre(
  inicio: () => string | null,
  inicial: { conservacion: NonNullable<Conservacion>; proximo: string | null; costo: number | null },
  proximoActual: () => string | null = () => null,
): FormularioCierre {
  const grupo: FormularioCierre = new FormGroup({
    fechaFin: new FormControl(hoyIso(), { nonNullable: true, validators: [Validators.required, noFutura, noAntesDe(inicio)] }),
    trabajo: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
    resultado: new FormControl<NonNullable<ResultadoMantenimiento>>('Operativo', { nonNullable: true }),
    costo: new FormControl<number | null>(inicial.costo, [Validators.min(0)]),
    estadoActivoId: new FormControl<string | null>(null, Validators.required),
    conservacion: new FormControl<NonNullable<Conservacion>>(inicial.conservacion, { nonNullable: true }),
    proximo: new FormControl(inicial.proximo ?? '', { nonNullable: true }),
  });
  // El próximo preventivo, si se cambia, debe ser posterior al término. Puede quedar el que ya tenía aunque esté
  // vencido (un correctivo no lo cumple), y sin reparación no se toca.
  const posterior = noAntesDe(() => sumarDias(grupo.controls.fechaFin.value, 1));
  grupo.controls.proximo.addValidators((c) =>
    !c.value || grupo.controls.resultado.value === 'SinReparacion' || c.value === proximoActual() ? null : posterior(c),
  );
  grupo.controls.fechaFin.valueChanges.subscribe(() => grupo.controls.proximo.updateValueAndValidity({ emitEvent: false }));
  grupo.controls.resultado.valueChanges.subscribe(() => grupo.controls.proximo.updateValueAndValidity({ emitEvent: false }));
  return grupo;
}

/** Lo que se envía a la API. El costo solo si lo hizo un proveedor; sin reparación no se cambia el próximo preventivo. */
export function cierreDesdeFormulario(grupo: FormularioCierre, conProveedor: boolean, proximoActual: string | null): CierreMantenimiento {
  const v = grupo.getRawValue();
  return {
    fechaFin: v.fechaFin,
    trabajoRealizado: v.trabajo.trim(),
    resultado: v.resultado,
    costo: conProveedor && v.costo !== null && String(v.costo) !== '' ? v.costo : null,
    estadoActivoId: v.estadoActivoId!,
    conservacion: v.conservacion,
    proximoMantenimiento: v.resultado === 'Operativo' ? v.proximo || null : proximoActual,
  };
}

function sumarDias(fecha: string, dias: number): string | null {
  if (!fecha) {
    return null;
  }
  const [a, m, d] = fecha.split('-').map(Number);
  const f = new Date(Date.UTC(a, m - 1, d + dias));
  return f.toISOString().slice(0, 10);
}

/**
 * Campos para cerrar un mantenimiento: término, costo (proveedor), trabajo realizado, resultado, estado y conservación
 * en que queda el activo y próximo preventivo. Al cambiar el resultado se propone el estado.
 */
@Component({
  selector: 'app-campos-cierre',
  imports: [ReactiveFormsModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <div class="cierre" [formGroup]="grupo()">
      <div class="rejilla">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Fecha de término</mat-label>
          <input matInput type="date" formControlName="fechaFin" [min]="minima() ?? ''" [max]="hoy" required />
          @if (grupo().controls.fechaFin.hasError('futura')) {
            <mat-error>No puede ser posterior a hoy.</mat-error>
          } @else if (grupo().controls.fechaFin.hasError('anterior')) {
            <mat-error>No puede ser anterior al inicio.</mat-error>
          } @else {
            <mat-error>Elija la fecha.</mat-error>
          }
        </mat-form-field>
        @if (conProveedor()) {
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Costo final</mat-label>
            <span matTextPrefix>S/&nbsp;</span>
            <input matInput type="number" min="0" step="0.01" formControlName="costo" class="numero" />
          </mat-form-field>
        }
      </div>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Trabajo realizado</mat-label>
        <textarea matInput formControlName="trabajo" rows="2" maxlength="1000" required></textarea>
        <mat-error>Describa el trabajo realizado.</mat-error>
      </mat-form-field>
      <div class="grupo">
        <span class="etiqueta" id="etiqueta-resultado">Resultado *</span>
        <mat-button-toggle-group formControlName="resultado" aria-labelledby="etiqueta-resultado" hideSingleSelectionIndicator>
          <mat-button-toggle value="Operativo">Quedó operativo</mat-button-toggle>
          <mat-button-toggle value="SinReparacion">Sin reparación</mat-button-toggle>
        </mat-button-toggle-group>
      </div>
      <div class="rejilla">
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Estado del activo después</mat-label>
          <mat-select formControlName="estadoActivoId" required>
            @for (e of estadosActivos(); track e.id) {
              <mat-option [value]="e.id">{{ e.nombre }}</mat-option>
            }
          </mat-select>
          <mat-hint>{{ grupo().controls.resultado.value === 'Operativo' ? 'Se propone «Operativo».' : 'Se propone «Inoperativo»; si ya no se usará, elija «De baja».' }}</mat-hint>
          <mat-error>Elija el estado.</mat-error>
        </mat-form-field>
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Conservación</mat-label>
          <mat-select formControlName="conservacion" required>
            @for (c of conservaciones; track c) {
              <mat-option [value]="c">{{ textoConservacion[c] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      @if (grupo().controls.resultado.value === 'Operativo') {
        <div class="grupo">
          <span class="etiqueta">Próximo preventivo</span>
          <div class="proximo">
            <mat-form-field subscriptSizing="dynamic" class="campo-proximo">
              <mat-label>Fecha</mat-label>
              <input matInput type="date" formControlName="proximo" />
              <mat-error>Debe ser posterior a la fecha de término.</mat-error>
            </mat-form-field>
            <div class="atajos" role="group" aria-label="Programar en">
              @for (m of atajos; track m.meses) {
                <button type="button" class="atajo" (click)="programar(m.meses)">{{ m.texto }}</button>
              }
              <button type="button" class="atajo" (click)="programar(null)">Sin programar</button>
            </div>
          </div>
          <span class="atenuado pista">{{ pistaProximo() }}</span>
        </div>
      }
    </div>
  `,
  styles: `
    .cierre { display: flex; flex-direction: column; gap: 14px; }
    .rejilla { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; align-items: start; }
    mat-form-field { width: 100%; }
    .grupo { display: flex; flex-direction: column; gap: 6px; }
    .etiqueta { font-size: 13px; font-weight: 500; }
    .proximo { display: flex; flex-wrap: wrap; gap: 8px 12px; align-items: center; }
    .campo-proximo { width: 200px; }
    .atajos { display: flex; flex-wrap: wrap; gap: 6px; }
    .atajo {
      height: 32px; padding: 0 12px; border: 1px solid var(--inv-borde-campo); border-radius: 999px;
      background: var(--inv-superficie); font: inherit; font-size: 13px; cursor: pointer; color: var(--inv-tinta);
    }
    .atajo:hover { border-color: var(--inv-primario); color: var(--inv-primario); }
    .pista { font-size: 12px; }
    .numero { text-align: right; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CamposCierreComponent {
  readonly grupo = input.required<FormularioCierre>();
  /** Estados del catálogo (con su grupo), para elegir y proponer. */
  readonly estados = input.required<readonly ElementoCatalogoDto[]>();
  readonly conProveedor = input(false);
  readonly tipo = input<TipoMantenimiento | null>(null);
  /** Inicio del mantenimiento (mínimo del término). */
  readonly minima = input<string | null>(null);

  protected readonly hoy = hoyIso();
  protected readonly conservaciones = Object.keys(TEXTO_CONSERVACION) as NonNullable<Conservacion>[];
  protected readonly textoConservacion = TEXTO_CONSERVACION;
  protected readonly atajos = [
    { meses: 3, texto: '3 meses' },
    { meses: 6, texto: '6 meses' },
    { meses: 12, texto: '1 año' },
  ];

  protected readonly estadosActivos = computed(() => this.estados().filter((e) => e.activo));

  protected readonly pistaProximo = computed(() =>
    this.tipo() === 'Preventivo'
      ? 'Se propone en 6 meses. Aparecerá en «Programados» y, cuando venza, en el Resumen.'
      : 'Se mantiene el que ya tenía; puede cambiarlo.',
  );

  constructor() {
    // Al llegar el catálogo, se propone el estado si todavía no hay uno.
    effect(() => {
      const estados = this.estados();
      const grupo = this.grupo();
      untracked(() => {
        if (!grupo.controls.estadoActivoId.value) {
          grupo.controls.estadoActivoId.setValue(estadoPropuesto(estados, grupo.controls.resultado.value));
        }
      });
    });

    // Al cambiar el resultado, se vuelve a proponer el estado.
    let suscripcion: { unsubscribe(): void } | undefined;
    effect(() => {
      const grupo = this.grupo();
      untracked(() => {
        suscripcion?.unsubscribe();
        suscripcion = grupo.controls.resultado.valueChanges.subscribe((r) => {
          const propuesto = estadoPropuesto(this.estados(), r);
          if (propuesto) {
            grupo.controls.estadoActivoId.setValue(propuesto);
          }
        });
      });
    });
    inject(DestroyRef).onDestroy(() => suscripcion?.unsubscribe());
  }

  protected programar(meses: number | null): void {
    const c = this.grupo().controls;
    c.proximo.setValue(meses === null ? '' : sumarMeses(c.fechaFin.value || this.hoy, meses));
    c.proximo.markAsTouched();
  }
}

