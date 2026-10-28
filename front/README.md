# Frontend: Inventario TIC (Angular 22)

Angular 22 con Angular Material, componentes standalone, signals y NgRx SignalStore.
Arquitectura y decisiones: [docs/ARQUITECTURA.md §7](../docs/ARQUITECTURA.md#7-frontend).

## Requisitos

- **Node.js 24 LTS** (mínimo 22.22.3 o 24.15). Compruébelo con `node -v`.
- La API corriendo en `https://localhost:7180` (carpeta `back`: `dotnet run --project src/Inventario.Api`).

## Primera vez

Desde la carpeta `front`:

```powershell
# 1. Instala las dependencias (exactas, según package-lock.json)
npm ci

# 2. Exporta el certificado HTTPS de desarrollo de .NET (el mismo de la API) a front/.certs/
#    para que ng serve funcione en https://localhost:4200 sin advertencias. Una sola vez por PC.
npm run certificado
```

## Día a día

```powershell
npm start          # https://localhost:4200 (las llamadas a /api se reenvían a la API)
npm test           # pruebas unitarias (Vitest)
npm run build      # compilación de producción en dist/
```

## Cliente de la API (generado)

`src/app/api/` se **genera** a partir del contrato OpenAPI de la API: no se edita a mano.
Cuando cambie la API, con la API corriendo:

```powershell
npm run api:actualizar   # descarga openapi/inventario-api.json y regenera src/app/api/
```

Si después de regenerar el frontend no compila, es porque la API cambió algo que el frontend usa: el error de
compilación indica qué ajustar. Ambos (el contrato y el código generado) se suben al repositorio, así los cambios
de la API se ven en cada commit.

## Estructura

```
src/app/
├─ api/            cliente generado (ng-openapi-gen); no se edita
├─ core/           se carga una sola vez
│  ├─ auth/        SesionStore (sesión y permisos), guards, directiva *siTienePermiso, códigos de permiso
│  ├─ http/        token antifalsificación (CSRF), manejo común de errores (401, contraseña temporal)
│  ├─ layout/      estructura con menú lateral, encabezado y selector de sede; menu.ts define el menú y sus rutas
│  └─ rutas.ts     rutas fijas y validación de la ruta de regreso después de iniciar sesión
├─ shared/ui/      piezas visuales sin lógica de negocio (avisos, logo, plantilla de las pantallas de acceso)
└─ features/       una carpeta por funcionalidad, con carga diferida
   ├─ cuenta/      iniciar sesión, olvidé mi contraseña, restablecer (enlace del correo), cambiar contraseña
   ├─ inicio/      resumen del inicio (cifras, estados, atención, fichas por completar, tabla por red) y página técnica /estado
   ├─ sedes/       sedes (solo superadmin): crear, renombrar, activar y desactivar
   ├─ usuarios/    usuarios: crear, editar, roles, activar/desactivar, restablecer contraseña, desbloquear
   ├─ roles/       roles de la sede con sus permisos
   ├─ catalogos/   áreas (por sede) y catálogos generales; un solo editor para todos
   ├─ activos/     inventario: lista con filtros, ficha técnica, formulario y fotos (galería, visor, reducción en el navegador)
   └─ pendiente/   lugar de las secciones del menú que aún no se construyen
```

## Pantallas

| Ruta | Quién | Qué hace |
|---|---|---|
| `/iniciar-sesion` | Sin sesión | Usuario o correo y contraseña. Muestra los mensajes de la API (bloqueo, cuenta o sede inactiva, sesión vencida). |
| `/olvide-contrasena` | Cualquiera | Pide el enlace por correo. Responde igual exista o no la cuenta. |
| `/restablecer-contrasena?usuario=…&token=…` | Cualquiera | Página del enlace del correo. |
| `/cambiar-contrasena` | Con sesión | Obligatoria con contraseña temporal; voluntaria desde el menú de la cuenta. |
| `/usuarios` | `Usuarios.Ver` (acciones con `Usuarios.Gestionar`) | Lista con búsqueda y filtro por estado. La contraseña temporal se muestra una sola vez. Los usuarios que no puede gestionar aparecen con un candado (mismas reglas que la API). |
| `/roles` | `Roles.Ver` (cambios con `Roles.Gestionar`) | Roles de la sede; el superadmin elige la sede arriba. Los permisos que no puede otorgar aparecen deshabilitados. |
| `/sedes` | `Sedes.Gestionar` | Solo el superadmin. |
| `/areas` | `Catalogos.GestionarSede` | Áreas de la sede (buscar o agregar en un solo campo); el superadmin elige la sede arriba. |
| `/establecimientos` | `Establecimientos.Gestionar` | Solo el superadmin. Lista con búsqueda y filtro por estado, registro y corrección, e importación desde Excel con la lista de filas a corregir. |
| `/catalogos` | `Catalogos.GestionarGlobales` | Tipos de activo (con la ficha técnica de cada uno), marcas, estados (con su grupo: en uso, en mantenimiento, fuera de servicio, en almacén o de baja), sistemas operativos, procesadores (con sus generaciones), antivirus o EDR y tipos de licencia. |
| `/` (Resumen) | `Activos.Ver` (sin él, una bienvenida) | Cuatro tarjetas de color (activos, renovar, próximos a renovar, garantía vencida), estado de los activos, antigüedad, requieren atención, activos por tipo, por establecimiento (con los que no tienen activos) y fichas por completar. El superadministrador con "Todas las sedes" ve la tabla por red; al tocar una red, o al elegirla arriba, ve su resumen. Cada cifra abre el inventario ya filtrado. |
| `/inventario` | `Activos.Ver` | Lista paginada en la API, con búsqueda y filtros que quedan en la dirección, y la miniatura de la foto principal. Los filtros que vienen del resumen (`?atencion=`, `?falta=`) se muestran como etiquetas que se quitan con ✕. En celular, tarjetas. |
| `/inventario/{id}` | `Activos.Ver` (Editar y Eliminar según permiso) | Ficha técnica de solo lectura; las secciones dependen de la ficha del tipo. Galería de fotos y visor a pantalla completa; con `Activos.Editar` se agregan fotos al momento y en el visor se marca la principal, se describe o se elimina. |
| `/inventario/nuevo`, `/inventario/{id}/editar` | `Activos.Crear`, `Activos.Editar` | Formulario de la ficha: establecimiento por búsqueda, secciones según el tipo, discos y RAM. Los catálogos largos se eligen escribiendo (`app-selector-catalogo`) y quien administra el catálogo puede agregar desde ahí. Pregunta antes de salir con cambios sin guardar. Con `Activos.Editar`, sección Fotos: tomar foto (celular), elegir o arrastrar archivos; se reducen en el navegador y se suben al guardar, después de la ficha. |
| `/` y demás secciones del menú | Con sesión | Estructura con menú según permisos. Las secciones aún no construidas muestran "próxima versión". |
| `/estado` | Cualquiera | Comprobación técnica de la conexión con la API. |

## Cómo se conecta con la API

- En desarrollo, `ng serve` reenvía `/api` a `https://localhost:7180` (`proxy.conf.json`). El navegador ve un solo
  sitio, así que la cookie de sesión (`HttpOnly`, `SameSite=Strict`) funciona sin CORS.
- En producción, la API publica el frontend compilado en su carpeta `wwwroot` (mismo sitio, sin proxy).
- Toda petición que cambia datos lleva la cabecera `X-XSRF-TOKEN` (protección CSRF). El token se pide a
  `GET /api/cuenta/csrf` y se renueva al iniciar o cerrar sesión (`core/http/csrf.interceptor.ts`).
- Las fotos se muestran con `<img src="/api/activos/{id}/fotos/{fotoId}/archivo">`: el navegador envía la cookie de
  sesión sola, porque es el mismo sitio. Antes de subirlas, `data/reducir-imagen.ts` las redibuja en un `<canvas>` a
  1600 px y 320 px (miniatura) en JPEG, lo que además quita la ubicación GPS. En la computadora no hay botón
  "Tomar foto"; en el celular, `capture="environment"` abre la cámara trasera (es un selector de archivos: no pide permisos de cámara).
- Las fuentes (IBM Plex) y los íconos (Material Icons) se sirven desde la propia aplicación: no depende de
  Google Fonts ni de internet en la red de la sede.
