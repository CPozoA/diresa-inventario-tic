import { HttpContextToken } from '@angular/common/http';

/**
 * Marca una petición para que un 401 no lleve a la pantalla de inicio de sesión.
 * La usa la consulta inicial de la sesión: un 401 ahí solo significa "todavía no ha iniciado sesión".
 */
export const SIN_REDIRECCION_401 = new HttpContextToken<boolean>(() => false);
