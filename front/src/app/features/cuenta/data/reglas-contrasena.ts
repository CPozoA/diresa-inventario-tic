/** Un requisito de la contraseña y si se cumple. */
export interface ReglaContrasena {
  readonly texto: string;
  readonly cumple: boolean;
}

/**
 * Evalúa la contraseña nueva con la misma política que la API (docs/ARQUITECTURA.md D17):
 * 10 caracteres o más, mayúscula, minúscula, número y al menos 4 caracteres distintos.
 * Sirve para guiar mientras se escribe; la API vuelve a validar al guardar.
 * @param actual Si se indica, también exige que la nueva sea distinta.
 */
export function evaluarContrasena(nueva: string, confirmacion: string, actual?: string): ReglaContrasena[] {
  const reglas: ReglaContrasena[] = [
    { texto: 'Al menos 10 caracteres', cumple: nueva.length >= 10 },
    { texto: 'Una letra mayúscula', cumple: /\p{Lu}/u.test(nueva) },
    { texto: 'Una letra minúscula', cumple: /\p{Ll}/u.test(nueva) },
    { texto: 'Un número', cumple: /\p{Nd}/u.test(nueva) },
    { texto: 'Al menos 4 caracteres distintos', cumple: new Set(nueva).size >= 4 },
  ];

  if (actual !== undefined) {
    reglas.push({ texto: 'Distinta de la actual', cumple: nueva.length > 0 && nueva !== actual });
  }

  reglas.push({ texto: 'Las dos coinciden', cumple: nueva.length > 0 && nueva === confirmacion });
  return reglas;
}

export function cumpleTodas(reglas: readonly ReglaContrasena[]): boolean {
  return reglas.every((r) => r.cumple);
}
