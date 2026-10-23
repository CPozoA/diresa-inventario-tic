import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { restablecerContrasena } from '../../../api/fn/cuenta/restablecer-contrasena';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { RUTAS } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { PlantillaAccesoComponent } from '../../../shared/ui/plantilla-acceso.component';
import { ReglasContrasenaComponent } from '../components/reglas-contrasena.component';
import { cumpleTodas, evaluarContrasena } from '../data/reglas-contrasena';

/** Código con el que la API rechaza un enlace vencido, usado o alterado. */
const CODIGO_ENLACE_INVALIDO = 'Cuenta.EnlaceInvalido';

type Estado = 'formulario' | 'enlaceInvalido' | 'listo';

/** Página del enlace que llega por correo: ?usuario=...&token=... */
@Component({
  selector: 'app-restablecer-contrasena',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    AvisoComponent,
    PlantillaAccesoComponent,
    ReglasContrasenaComponent,
  ],
  templateUrl: './restablecer-contrasena.page.html',
  styleUrl: './cuenta.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RestablecerContrasenaPage {
  private readonly api = inject(Api);

  /** Identificador del usuario (parámetro ?usuario= del enlace). */
  readonly usuario = input<string>();

  /** Token del enlace (parámetro ?token=). */
  readonly token = input<string>();

  protected readonly rutas = RUTAS;
  protected readonly formulario = inject(NonNullableFormBuilder).group({
    nueva: ['', Validators.required],
    confirmacion: ['', Validators.required],
  });

  private readonly valores = toSignal(this.formulario.valueChanges, { initialValue: this.formulario.getRawValue() });
  protected readonly reglas = computed(() => evaluarContrasena(this.valores().nueva ?? '', this.valores().confirmacion ?? ''));
  protected readonly cumple = computed(() => cumpleTodas(this.reglas()));

  protected readonly enviando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);
  private readonly resultado = signal<Estado | null>(null);

  /** Sin usuario o token en la dirección, el enlace está incompleto: se trata como inválido. */
  protected readonly estado = computed<Estado>(
    () => this.resultado() ?? (this.usuario() && this.token() ? 'formulario' : 'enlaceInvalido'),
  );

  protected async guardar(): Promise<void> {
    const usuarioId = this.usuario();
    const token = this.token();

    if (!this.cumple() || this.enviando() || !usuarioId || !token) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.error.set(null);

    try {
      await this.api.invoke(restablecerContrasena, {
        body: { usuarioId, token, contrasenaNueva: this.formulario.getRawValue().nueva },
      });
      this.resultado.set('listo');
    } catch (error: unknown) {
      const errorApi = aErrorApi(error);
      if (errorApi.codigo === CODIGO_ENLACE_INVALIDO) {
        this.resultado.set('enlaceInvalido');
      } else {
        this.error.set(errorApi);
      }
    } finally {
      this.enviando.set(false);
    }
  }
}
