import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';

import { Api } from '../../../api/api';
import { olvideContrasena } from '../../../api/fn/cuenta/olvide-contrasena';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { RUTAS } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { PlantillaAccesoComponent } from '../../../shared/ui/plantilla-acceso.component';

/**
 * Solicita el enlace para restablecer la contraseña. La API responde igual exista o no el correo,
 * así que la pantalla tampoco revela si hay una cuenta con ese correo.
 */
@Component({
  selector: 'app-olvide-contrasena',
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
  templateUrl: './olvide-contrasena.page.html',
  styleUrl: './cuenta.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OlvideContrasenaPage {
  private readonly api = inject(Api);

  protected readonly rutas = RUTAS;
  protected readonly formulario = inject(NonNullableFormBuilder).group({
    correo: ['', [Validators.required, Validators.email]],
  });

  protected readonly enviando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);
  protected readonly enviadoA = signal<string | null>(null);

  protected async enviar(): Promise<void> {
    if (this.formulario.invalid || this.enviando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.error.set(null);

    try {
      const correo = this.formulario.getRawValue().correo.trim();
      await this.api.invoke(olvideContrasena, { body: { correo } });
      this.enviadoA.set(correo);
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.enviando.set(false);
    }
  }

  protected usarOtroCorreo(): void {
    this.enviadoA.set(null);
    this.formulario.reset();
  }
}
