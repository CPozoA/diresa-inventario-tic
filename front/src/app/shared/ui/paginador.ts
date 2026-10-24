import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

/** Textos del paginador de Material en español. Se provee en las pantallas que paginan. */
@Injectable()
export class PaginadorEnEspanol extends MatPaginatorIntl {
  override itemsPerPageLabel = 'Filas por página';
  override nextPageLabel = 'Página siguiente';
  override previousPageLabel = 'Página anterior';
  override firstPageLabel = 'Primera página';
  override lastPageLabel = 'Última página';

  override getRangeLabel = (pagina: number, tamano: number, total: number): string => {
    if (total === 0) {
      return '0 de 0';
    }
    const desde = pagina * tamano + 1;
    const hasta = Math.min((pagina + 1) * tamano, total);
    return `${desde} a ${hasta} de ${total}`;
  };
}
