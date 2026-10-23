import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';

import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { MOTIVO_SESION_VENCIDA, RUTAS, rutaInternaSegura } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { PlantillaAccesoComponent } from '../../../shared/ui/plantilla-acceso.component';

/** Inicio de sesión con usuario (o correo) y contraseña. */
@Component({
  selector: 'app-iniciar-sesion',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    AvisoComponent,
    PlantillaAccesoComponent,
  ],
  templateUrl: './iniciar-sesion.page.html',
  styleUrl: './cuenta.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IniciarSesionPage {
  private readonly sesion = inject(SesionStore);
  private readonly router = inject(Router);

  /** Ruta a la que volver después de iniciar sesión (parámetro ?volver=). */
  readonly volver = input<string>();

  /** Por qué se llegó aquí (parámetro ?motivo=), por ejemplo, la sesión venció. */
  readonly motivo = input<string>();

  protected readonly rutas = RUTAS;
  protected readonly formulario = inject(NonNullableFormBuilder).group({
    usuario: ['', Validators.required],
    contrasena: ['', Validators.required],
  });

  protected readonly enviando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);
  protected readonly claveVisible = signal(false);
  protected readonly sesionVencida = computed(() => this.motivo() === MOTIVO_SESION_VENCIDA && this.error() === null);

  /** Bloqueo por intentos: es una advertencia, no un error de datos. */
  protected readonly tonoError = computed(() => (this.error()?.codigo === 'Cuenta.Bloqueada' ? 'alerta' : 'mal'));

  protected async ingresar(): Promise<void> {
    if (this.formulario.invalid || this.enviando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.error.set(null);

    try {
      const { usuario, contrasena } = this.formulario.getRawValue();
      const sesion = await this.sesion.iniciarSesion(usuario.trim(), contrasena);
      const destino = sesion.debeCambiarContrasena ? `/${RUTAS.cambiarContrasena}` : rutaInternaSegura(this.volver());
      await this.router.navigateByUrl(destino);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.enviando.set(false);
    }
  }
}
