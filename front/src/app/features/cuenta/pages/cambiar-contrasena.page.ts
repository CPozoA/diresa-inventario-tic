import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';

import { SesionStore } from '../../../core/auth/sesion.store';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { RUTAS } from '../../../core/rutas';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { LogoComponent } from '../../../shared/ui/logo.component';
import { ReglasContrasenaComponent } from '../components/reglas-contrasena.component';
import { cumpleTodas, evaluarContrasena } from '../data/reglas-contrasena';

/**
 * Cambio de contraseña del usuario conectado. Es obligatorio mientras tenga una contraseña temporal
 * (entonces no hay menú ni "Cancelar": solo puede cambiarla o cerrar sesión).
 */
@Component({
  selector: 'app-cambiar-contrasena',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    AvisoComponent,
    LogoComponent,
    ReglasContrasenaComponent,
  ],
  templateUrl: './cambiar-contrasena.page.html',
  styleUrls: ['./cuenta.scss', './cambiar-contrasena.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CambiarContrasenaPage {
  protected readonly sesion = inject(SesionStore);
  private readonly router = inject(Router);
  private readonly avisos = inject(MatSnackBar);

  protected readonly obligatorio = this.sesion.debeCambiarContrasena;
  protected readonly formulario = inject(NonNullableFormBuilder).group({
    actual: ['', Validators.required],
    nueva: ['', Validators.required],
    confirmacion: ['', Validators.required],
  });

  private readonly valores = toSignal(this.formulario.valueChanges, { initialValue: this.formulario.getRawValue() });
  protected readonly reglas = computed(() => {
    const v = this.valores();
    return evaluarContrasena(v.nueva ?? '', v.confirmacion ?? '', v.actual ?? '');
  });
  protected readonly listo = computed(() => (this.valores().actual ?? '').length > 0 && cumpleTodas(this.reglas()));

  protected readonly enviando = signal(false);
  protected readonly error = signal<ErrorApi | null>(null);

  protected readonly descripcionUsuario = computed(() => {
    const s = this.sesion.sesion();
    if (!s) {
      return '';
    }
    return s.esSuperAdmin ? `${s.nombreCompleto} · Superadministrador` : `${s.nombreCompleto} · ${s.sede?.nombre ?? ''}`;
  });

  protected async guardar(): Promise<void> {
    if (!this.listo() || this.enviando()) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.error.set(null);

    try {
      const { actual, nueva } = this.formulario.getRawValue();
      await this.sesion.cambiarContrasena(actual, nueva);
      this.avisos.open('Su contraseña se actualizó.', 'Cerrar', { duration: 5000 });
      await this.router.navigateByUrl('/');
    } catch (error: unknown) {
      this.error.set(aErrorApi(error));
    } finally {
      this.enviando.set(false);
    }
  }

  protected async cerrarSesion(): Promise<void> {
    await this.sesion.cerrarSesion();
    await this.router.navigate(['/', RUTAS.iniciarSesion]);
  }
}
