# Frontend: Inventario TIC (Angular 22)

Aquí vivirá la aplicación Angular. Se crea en el **Paso 5**.

```
front/src/app/
├─ core/       sesión, interceptores HTTP, guards por permiso, layout
├─ shared/     componentes visuales y pipes reutilizables, sin lógica de negocio
├─ features/   una carpeta por funcionalidad, con carga diferida
│  ├─ resumen/  activos/  licencias/  sedes/  usuarios/  roles/  catalogos/
│  └─ <funcionalidad>/  pages/ · components/ · data/ · <funcionalidad>.routes.ts
└─ api/        cliente generado desde el OpenAPI del backend (no se edita a mano)
```

Detalle y justificación: [docs/ARQUITECTURA.md §7](../docs/ARQUITECTURA.md#7-frontend).

Bosquejos de referencia: los PNG en la carpeta raíz del proyecto.
