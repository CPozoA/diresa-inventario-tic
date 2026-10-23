import { RolDto } from '../../api/models/rol-dto';

/**
 * Las mismas reglas contra el escalamiento de privilegios que aplica la API (docs/ARQUITECTURA.md §6.3),
 * usadas solo para deshabilitar de antemano lo que la API rechazaría. La API siempre vuelve a validar.
 */

/** Un rol se puede otorgar si todos sus permisos los tiene quien otorga. El de Administrador de sede, solo el superadmin. */
export function puedeOtorgarRol(rol: RolDto, permisosPropios: ReadonlySet<string>, esSuperAdmin: boolean): boolean {
  if (esSuperAdmin) {
    return true;
  }

  return !rol.esDelSistema && rol.permisos.every((p) => permisosPropios.has(p));
}

/** Motivo por el que no se puede gestionar a un usuario, o nulo si se puede. */
export function motivoNoGestionable(
  objetivo: { readonly id: string; readonly roles: readonly { readonly id: string }[] },
  rolesDeLaSede: readonly RolDto[],
  actual: { readonly usuarioId: string | null; readonly esSuperAdmin: boolean; readonly permisos: ReadonlySet<string> },
): string | null {
  if (objetivo.id === actual.usuarioId) {
    return 'Su propia cuenta se gestiona desde el menú de su cuenta.';
  }

  if (actual.esSuperAdmin) {
    return null;
  }

  const roles = objetivo.roles
    .map((r) => rolesDeLaSede.find((x) => x.id === r.id))
    .filter((r): r is RolDto => r !== undefined);

  if (roles.some((r) => r.esDelSistema)) {
    return 'A un administrador de sede solo lo gestiona el superadministrador.';
  }

  const tieneMas = roles.some((r) => r.permisos.some((p) => !actual.permisos.has(p)));
  return tieneMas ? 'Este usuario tiene permisos que usted no tiene.' : null;
}
