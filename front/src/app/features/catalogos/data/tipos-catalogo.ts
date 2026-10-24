import { inject, resource } from '@angular/core';

import { Api } from '../../../api/api';
import { listarTiposCatalogo } from '../../../api/fn/catalogos/listar-tipos-catalogo';

/** Catálogos del sistema y si el usuario puede modificarlos (se usa dentro de un componente). */
export function recursoTiposCatalogo() {
  const api = inject(Api);
  return resource({ loader: () => api.invoke(listarTiposCatalogo) });
}
