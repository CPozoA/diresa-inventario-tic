import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors, withNoXsrfProtection } from '@angular/common/http';
import localeEsPe from '@angular/common/locales/es-PE';
import { ApplicationConfig, DEFAULT_CURRENCY_CODE, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { csrfInterceptor } from './core/http/csrf.interceptor';
import { erroresInterceptor } from './core/http/errores.interceptor';

// Formato regional de Perú: fechas dd/mm/aaaa, separadores y moneda S/.
registerLocaleData(localeEsPe);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Formularios de Material con borde (outline) en toda la aplicación.
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
    provideHttpClient(
      withFetch(),
      // El token antifalsificación lo maneja csrfInterceptor (pide uno nuevo y reintenta si venció),
      // por eso se desactiva el mecanismo automático de Angular.
      withNoXsrfProtection(),
      // Orden: el primero envuelve a los demás. Los errores se tratan al final, después del posible reintento CSRF.
      withInterceptors([erroresInterceptor, csrfInterceptor]),
    ),
    // Rutas relativas (/api/...): en desarrollo las reenvía el proxy de ng serve; en producción la API sirve el frontend.
    provideApiConfiguration(''),
    { provide: LOCALE_ID, useValue: 'es-PE' },
    { provide: DEFAULT_CURRENCY_CODE, useValue: 'PEN' },
  ],
};
