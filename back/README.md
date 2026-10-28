# Backend: Inventario TIC (.NET 10)

API en ASP.NET Core con Clean Architecture organizada por funcionalidades.
Detalle y justificación: [docs/ARQUITECTURA.md §5](../docs/ARQUITECTURA.md#5-backend).

## Estructura

```
back/
├─ Inventario.slnx               solución
├─ global.json                   fija el SDK .NET 10 (admite parches más nuevos)
├─ Directory.Build.props         configuración común: net10.0, nullable, analizadores, advertencias = errores
├─ Directory.Packages.props      versiones de paquetes NuGet centralizadas
├─ .config/dotnet-tools.json     herramientas locales (dotnet-ef)
└─ src/
   ├─ Inventario.Domain/         entidades y reglas puras, sin dependencias
   │  ├─ Common/                 Entidad, EntidadAuditable, IPerteneceASede, DominioException
   │  ├─ Catalogos/              Area, Ubicacion (por sede); TipoActivo, Marca, EstadoActivo, SistemaOperativo,
   │  │                          TipoLicencia (generales); CatalogosIniciales
   │  ├─ Sedes/                  Sede, SedesIniciales (las 7 redes de salud)
   │  ├─ Seguridad/              Permisos (catálogo), Rol, RolPermiso, TipoRol, RolesDelSistema
   │  └─ Usuarios/               Usuario, UsuarioRol
   ├─ Inventario.Application/    casos de uso por funcionalidad
   │  ├─ Common/                 Result, ErrorAplicacion, ICommand/IQuery, IUsuarioActual, IAppDbContext,
   │  │                          IServicioIdentidad, IServicioCorreo, IEnlacesAplicacion,
   │  │                          ReglasDeGestion (anti-escalamiento), GeneradorContrasenaTemporal
   │  ├─ Cuenta/                 iniciar/cerrar sesión, sesión actual, cambiar y recuperar contraseña
   │  ├─ Catalogos/              una API para todos los catálogos (DefinicionesCatalogo.cs los describe)
   │  ├─ Sedes/                  listar, crear, editar, activar/desactivar
   │  ├─ Roles/                  listar roles y permisos, crear, editar, eliminar
   │  └─ Usuarios/               listar, obtener, crear, editar, asignar roles, activar/desactivar,
   │                             restablecer contraseña, desbloquear
   ├─ Inventario.Infrastructure/ persistencia, Identity, correo
   │  ├─ Identidad/              CuentaUsuario, ServicioIdentidad, FabricaPrincipal (claims), errores en español
   │  ├─ Correo/                 CorreoEnCarpeta (desarrollo), CorreoSmtp (producción)
   │  ├─ Enlaces/                enlaces hacia el frontend (restablecer contraseña)
   │  └─ Persistencia/           AppDbContext, Configuraciones/, Interceptores/, Inicializacion/, Migraciones/
   └─ Inventario.Api/            endpoints (Minimal APIs), seguridad, errores
      ├─ Common/                 traducción de resultados a respuestas HTTP; DominioException → 400
      ├─ Endpoints/              /api/salud, /api/cuenta/*, /api/sedes/*, /api/roles/*, /api/usuarios/*
      └─ Seguridad/              cookie de sesión, protección CSRF, permisos por endpoint, límite de intentos,
                                 cambio obligatorio de contraseña
```

```
tests/
├─ Inventario.Domain.Tests/          reglas del dominio (sedes, roles, permisos, usuarios); sin base de datos
├─ Inventario.ArchitectureTests/     dependencias entre capas, handlers internos, constructores para EF Core
└─ Inventario.Api.IntegrationTests/  API en memoria contra SQL Server: sesión, permisos y aislamiento entre sedes
```

## Base de datos

| Esquema | Tablas |
|---------|--------|
| `organizacion` | `Sedes` |
| `seguridad` | `Usuarios`, `Cuentas` (+ `CuentaClaims`, `CuentaLogins`, `CuentaTokens`), `Roles`, `RolPermisos`, `UsuarioRoles`, `ClavesProteccionDatos` |

Al arrancar, la API carga los **datos iniciales** si faltan: las 7 sedes, los roles del sistema
(Superadministrador y Administrador de sede) y los roles Editor y Consulta de cada sede.
No duplica nada si se ejecuta varias veces.

## Primera vez: crear la base de datos

Requisitos: SDK de .NET 10 y SQL Server Express en `.\SQLEXPRESS` con autenticación de Windows.
La cadena de conexión de desarrollo está en `src/Inventario.Api/appsettings.Development.json`.
No lleva contraseña, por eso puede estar en el repositorio.

Desde la carpeta `back`:

```powershell
# 1. Instala la herramienta de migraciones (dotnet-ef) declarada en .config/dotnet-tools.json
dotnet tool restore

# 2. Compila
dotnet build

# 3. Crea la migración inicial (genera archivos en Infrastructure/Persistencia/Migraciones)
dotnet ef migrations add Inicial --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones

# 4. Crea la base de datos InventarioTIC y sus tablas
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api

# 5. Arranca la API (carga los datos iniciales)
dotnet run --project src/Inventario.Api --launch-profile https
```

Luego abre:

- <https://localhost:7180/scalar>: documentación interactiva de la API (solo en desarrollo).
- <https://localhost:7180/api/sedes>: debe devolver las 7 sedes.
- <https://localhost:7180/api/salud>: comprobación rápida.

Si el navegador advierte por el certificado de desarrollo, ejecuta una vez `dotnet dev-certs https --trust`.

## Paso 3: superadministrador e inicio de sesión

### 1. Configurar tu cuenta de superadministrador (una sola vez)

Los datos van en los *user secrets* de tu computadora (fuera del repositorio). La contraseña inicial se genera al azar
y es **temporal**: el sistema te obligará a cambiarla al entrar. Desde la carpeta `back`, en PowerShell:

```powershell
# Genera una contraseña temporal aleatoria que cumple la política (mayúscula, minúscula, número, 10+ caracteres)
$car = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'.ToCharArray()
$clave = 'Tmp-' + (-join (1..12 | ForEach-Object { $car | Get-Random })) + '7'

dotnet user-secrets set "Inicializacion:SuperAdmin:NombreUsuario" "tu.usuario" --project src/Inventario.Api
dotnet user-secrets set "Inicializacion:SuperAdmin:Correo" "tu.correo@institucion.gob.pe" --project src/Inventario.Api
dotnet user-secrets set "Inicializacion:SuperAdmin:ContrasenaInicial" $clave --project src/Inventario.Api

Write-Host "Contraseña temporal: $clave"
```

Anota la contraseña temporal que muestra el último comando. Para ver lo guardado: `dotnet user-secrets list --project src/Inventario.Api`.

Recomendación: usa un nombre de usuario personal y poco obvio (no `admin` ni `superadmin`), porque con el bloqueo
tras 3 intentos alguien podría bloquear a propósito una cuenta de nombre predecible.

### 2. Compilar, verificar el modelo y arrancar

```powershell
dotnet build
dotnet ef migrations has-pending-model-changes --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
dotnet run --project src/Inventario.Api --launch-profile https
```

- El segundo comando debe decir que **no hay cambios pendientes** en el modelo. Si dice que los hay, crea una migración
  (`dotnet ef migrations add Paso3 ...`) y aplícala (`dotnet ef database update ...`) antes de arrancar.
- Al arrancar, el registro debe mostrar: `Superadministrador 'tu.usuario' creado. Deberá cambiar la contraseña temporal...`

### 3. Probar en Scalar (<https://localhost:7180/scalar>)

> **Desde el Paso 5A**, todo `POST`, `PUT` o `DELETE` exige el token antifalsificación (protección CSRF, D24).
> En Scalar: llama a `GET /api/cuenta/csrf`, copia el valor de `token` y agrégalo en la pestaña **Headers** de cada
> petición que cambia datos, como `X-XSRF-TOKEN: <token>`. Después de iniciar o cerrar sesión, pide un token nuevo
> (está ligado al usuario). Sin token, la API responde 400 `Seguridad.TokenCsrfInvalido`. El frontend hace todo esto solo.

| # | Petición | Resultado esperado |
|---|----------|--------------------|
| 1 | `POST /api/cuenta/iniciar-sesion` con `{"usuario": "tu.usuario", "contrasena": "<temporal>"}` | 200, `debeCambiarContrasena: true` |
| 2 | `GET /api/sedes` | 403, código `Cuenta.DebeCambiarContrasena` |
| 3 | `POST /api/cuenta/cambiar-contrasena` con `{"contrasenaActual": "...", "contrasenaNueva": "..."}` | 204 |
| 4 | `GET /api/cuenta/sesion` | 200, `debeCambiarContrasena: false`, `esSuperAdmin: true`, todos los permisos |
| 5 | `GET /api/sedes` | 200, las 7 sedes |
| 6 | `POST /api/cuenta/cerrar-sesion` y luego `GET /api/sedes` | 204 y luego 401 |

**Bloqueo y recuperación** (opcional; bloquea tu cuenta 15 minutos):

| # | Petición | Resultado esperado |
|---|----------|--------------------|
| 7 | `iniciar-sesion` con contraseña incorrecta, 3 veces | 401, 401 y al tercero 422 `Cuenta.Bloqueada` |
| 8 | `POST /api/cuenta/olvide-contrasena` con `{"correo": "tu.correo@..."}` | 204 y un archivo nuevo en `src/Inventario.Api/correos-dev/` |
| 9 | Abre ese archivo: el enlace trae `usuario=...&token=...`. Llama a `POST /api/cuenta/restablecer-contrasena` con `{"usuarioId": "...", "token": "...", "contrasenaNueva": "..."}` | 204: contraseña nueva y cuenta desbloqueada |

## Paso 4B: gestión de sedes, roles y usuarios

No cambia el modelo de datos: **no hay migración nueva**.

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET/POST /api/sedes`, `PUT /api/sedes/{id}`, `POST /api/sedes/{id}/activar\|desactivar` | `Sedes.Gestionar` (solo superadmin) | Una sede nueva nace con los roles Editor y Consulta |
| `GET /api/roles?sedeId=` y `GET /api/roles/permisos` | `Roles.Ver` | El superadmin indica la sede; los demás ven la suya |
| `POST /api/roles`, `PUT/DELETE /api/roles/{id}` | `Roles.Gestionar` | Solo con permisos que uno tiene; no se borra un rol asignado |
| `GET /api/usuarios?sedeId=&buscar=` y `GET /api/usuarios/{id}` | `Usuarios.Ver` | Nunca muestra al superadmin |
| `POST /api/usuarios` | `Usuarios.Gestionar` | Devuelve la **contraseña temporal una sola vez** |
| `PUT /api/usuarios/{id}`, `PUT /api/usuarios/{id}/roles` | `Usuarios.Gestionar` | Datos y correo; reemplazo de roles |
| `POST /api/usuarios/{id}/activar\|desactivar\|restablecer-contrasena\|desbloquear` | `Usuarios.Gestionar` | Desactivar y restablecer cierran sus sesiones |

Reglas contra el escalamiento: [ARQUITECTURA.md §6.3](../docs/ARQUITECTURA.md#63-roles) (reglas 1 a 11).

**Prueba manual en Scalar** (con tu sesión de superadmin):

| # | Petición | Resultado esperado |
|---|----------|--------------------|
| 1 | `GET /api/sedes` y copia el `id` de Huanta | 200 |
| 2 | `GET /api/roles?sedeId=<id de Huanta>` y copia el `id` de "Administrador de sede" | 200: Administrador de sede, Consulta y Editor |
| 3 | `POST /api/usuarios` con `{"sedeId": "<id de Huanta>", "nombreUsuario": "admin.huanta", "correo": "...", "nombres": "...", "apellidos": "...", "rolIds": ["<id de Administrador de sede>"]}` | 201 con `contrasenaTemporal` (anótala: no se vuelve a mostrar) |
| 4 | En otra ventana privada, inicia sesión como `admin.huanta` y cambia la contraseña | 200 y 204 |
| 5 | Como `admin.huanta`: `GET /api/usuarios` | 200, solo usuarios de Huanta |
| 6 | Como `admin.huanta`: `POST /api/roles` con `{"nombre": "X", "permisos": ["Sedes.Gestionar"]}` | 403, `Gestion.PermisosQueNoTiene` |

## Paso 6B: catálogos

Este paso **sí cambia el modelo**: agrega el esquema `catalogos` con siete tablas. Desde la carpeta `back`, con la API detenida:

```powershell
dotnet ef migrations add Catalogos --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Al arrancar, la API carga los valores iniciales de los catálogos generales (tipos de activo, marcas, estados, sistemas
operativos y tipos de licencia). Las pruebas automáticas aplican la migración solas en su base de pruebas.

| Endpoint | Quién | Qué hace |
|---|---|---|
| `GET /api/catalogos` | Con sesión | Catálogos disponibles y si puede modificarlos |
| `GET /api/catalogos/{tipo}?sedeId=&incluirInactivos=` | Con sesión | Elementos (por omisión, solo activos) |
| `POST /api/catalogos/{tipo}`, `PUT /api/catalogos/{tipo}/{id}` | Permiso del catálogo | Agregar y renombrar |
| `POST /api/catalogos/{tipo}/{id}/activar\|desactivar` | Permiso del catálogo | No se eliminan: se desactivan |

`tipo`: `areas` (por sede, `Catalogos.GestionarSede`) · `tipos-activo`, `marcas`, `estados-activo`,
`sistemas-operativos`, `tipos-licencia` (generales, `Catalogos.GestionarGlobales`). El catálogo de ubicaciones se retiró
en el Paso 6D (la ubicación física es texto en cada activo, D40).

## Paso 6D: establecimientos

Este paso **cambia el modelo**: agrega la tabla `organizacion.Establecimientos` y **elimina** `catalogos.Ubicaciones`
(se pierden las ubicaciones de prueba que hayas creado). También agrega el paquete **ClosedXML** (Excel). Desde `back`,
con la API detenida:

```powershell
dotnet restore
dotnet ef migrations add Establecimientos --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

| Endpoint | Quién | Qué hace |
|---|---|---|
| `GET /api/establecimientos?sedeId=&buscar=&incluirInactivos=&maximo=` | Con sesión | Lista (un usuario de sede solo ve los de su red). `buscar`: código único, Id_Establecimiento o parte del nombre |
| `POST /api/establecimientos`, `PUT /api/establecimientos/{id}` | `Establecimientos.Gestionar` | Registrar y corregir (la red no cambia) |
| `POST /api/establecimientos/{id}/activar\|desactivar` | `Establecimientos.Gestionar` | No se eliminan: se desactivan |
| `GET /api/establecimientos/plantilla` | `Establecimientos.Gestionar` | Plantilla de Excel |
| `POST /api/establecimientos/importar` (archivo `.xlsx`, campo `archivo`) | `Establecimientos.Gestionar` | Importa; si una fila tiene errores no guarda nada y devuelve la lista de filas a corregir |

`Establecimientos.Gestionar` es exclusivo del superadministrador. Detalle de columnas y reglas: `docs/ARQUITECTURA.md` §5.9.

## Paso 7A: activos

Este paso **cambia el modelo**: agrega el esquema `inventario` con las tablas `Activos`, `Discos` y `ModulosRam`. Desde `back`,
con la API detenida:

```powershell
dotnet ef migrations add Activos --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET /api/activos?sedeId=&establecimientoId=&tipoActivoId=&areaId=&estadoActivoId=&garantia=&renovacion=&buscar=&pagina=&tamanoPagina=` | `Activos.Ver` | Lista paginada (`garantia`: `Vigente`, `Vencida`, `SinDato`; `renovacion`: `Vigente`, `ProximoARenovar`, `Renovar`, `SinDato`) |
| `GET /api/activos/{id}` | `Activos.Ver` | Ficha completa con discos, RAM, totales y situación a hoy |
| `GET /api/activos/listas` | `Activos.Ver` | Listas fijas de la ficha con sus textos |
| `GET /api/activos/serie?numero=&excluirId=` | `Activos.Ver` | Activos de la sede con ese número de serie |
| `POST /api/activos`, `PUT /api/activos/{id}` | `Activos.Crear`, `Activos.Editar` | Registrar y reemplazar la ficha (incluidos discos y RAM) |
| `DELETE /api/activos/{id}` | `Activos.Eliminar` | Eliminar un registro hecho por error |

Para probar en Scalar, el cuerpo de `POST /api/activos` tiene la forma
`{ "establecimientoId": "…", "generales": { "codigoPatrimonial": "PAT-0001", "tipoActivoId": "…", "marcaId": "…", "modelo": "…", "areaId": "…", "estadoActivoId": "…", "conservacion": "Bueno" }, "adquisicion": { … }, "tecnicos": { … }, "discos": [ … ], "modulosRam": [ … ] }`.
Los identificadores salen de `GET /api/establecimientos` y `GET /api/catalogos/{tipo}`. Reglas: `docs/ARQUITECTURA.md` §5.10.

## Paso 7C-1: ficha técnica por tipo de activo (D43)

Este paso **cambia el modelo**: agrega la columna `Ficha` a `catalogos.TiposActivo`. Desde `back`, con la API detenida:

```powershell
dotnet ef migrations add FichaTecnica --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Al arrancar, la API asigna la ficha a los tipos que no la tienen según su nombre (verás en el registro cuántos completó).

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `PUT /api/catalogos/tipos-activo/{id}/ficha` | `Catalogos.GestionarGlobales` | Cuerpo `{ "ficha": "Computo" }` (`Computo`, `Red` o `Ninguna`) |

`GET /api/catalogos/tipos-activo` incluye `ficha` en cada tipo, y `GET /api/activos/{id}` la del tipo del activo. Al registrar o
editar, un tipo `Red` solo acepta nombre de equipo, IP y MAC; un tipo `Ninguna`, ningún dato técnico, disco ni RAM
(400 `Activos.DatosNoCorresponden`).

## Paso 7C-3: procesador y generación de lista (D44)

Este paso **cambia el modelo**: agrega el catálogo `catalogos.Procesadores` y, en `inventario.Activos`, reemplaza las columnas
de texto `Procesador` y `VelocidadGeneracion` por `ProcesadorId` y `GeneracionProcesador`. **Lo que se había escrito en esas
dos columnas se pierde** (solo había datos de prueba). Desde `back`, con la API detenida:

```powershell
dotnet ef migrations add Procesadores --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

EF avisa que la migración puede perder datos: es lo esperado. Al arrancar, la API carga los procesadores iniciales.

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET /api/catalogos/procesadores` | Con sesión | Familias con `generaciones` (`IntelCore`, `IntelCoreUltra`, `AmdRyzen` o `Ninguna`) |
| `PUT /api/catalogos/procesadores/{id}/generaciones` | `Catalogos.GestionarGlobales` | Cuerpo `{ "generaciones": "AmdRyzen" }` |
| `GET /api/activos/listas` | `Activos.Ver` | Incluye `generacionesProcesador`: las opciones de cada familia |

En la ficha, `tecnicos` lleva `procesadorId` y `generacionProcesador` (el código: `"13"`, `"5000"`). La búsqueda del
inventario (`?buscar=`) también encuentra por IP y por nombre de equipo.

## Paso 7C-4: más listas y sugerencias en la ficha (D45)

Este paso **cambia el modelo**: agrega el catálogo `catalogos.Antivirus` y, en `inventario.Activos`, reemplaza las columnas de
texto `TarjetaGrafica`, `Antivirus`, `PuertosPrincipales` y `AccesoriosIncluidos` por `TipoGrafica`, `ModeloGrafica`,
`AntivirusId`, `Puertos` y `Accesorios`. **Lo escrito en esas cuatro columnas se pierde** (solo había datos de prueba).
Desde `back`, con la API detenida:

```powershell
dotnet ef migrations add ListasFicha --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET /api/catalogos/antivirus` | Con sesión | Catálogo general de antivirus o EDR |
| `GET /api/activos/sugerencias?campo=&texto=&marcaId=&sistemaOperativoId=&establecimientoId=` | `Activos.Ver` | Hasta 10 valores ya registrados en la red, los más usados primero. `campo`: `Modelo`, `Proveedor`, `DominioGrupo`, `VersionSistemaOperativo`, `UbicacionFisica`, `MarcaModeloDisco` o `MarcaModeloRam` |
| `GET /api/activos/listas` | `Activos.Ver` | Incluye `tiposGrafica`, `puertos`, `accesorios` y `pantallasPulgadas` |

En la ficha, `tecnicos` lleva `tipoGrafica` (`Integrada` o `Dedicada`), `modeloGrafica` (solo si es dedicada),
`antivirusId`, y `puertos` y `accesorios` como listas (`["UsbA", "Hdmi"]`).

## Paso 7D-1: fotos de los activos en la API (D38, D47)

Este paso **cambia el modelo**: agrega la tabla `inventario.FotosActivo`. Desde `back`, con la API detenida:

```powershell
dotnet ef migrations add FotosActivo --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Los archivos se guardan en la carpeta `Almacenamiento:CarpetaFotos`. En desarrollo es `fotos-dev`, dentro de
`src/Inventario.Api` (está en `.gitignore`; las pruebas usan `fotos-pruebas`). En el servidor se define con una ruta
absoluta fuera de la carpeta publicada, por ejemplo `Almacenamiento__CarpetaFotos=D:\InventarioTIC\fotos`, y se incluye
en las copias de seguridad.

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET /api/activos/{id}/fotos` | `Activos.Ver` | Fotos del activo, la principal primero |
| `GET /api/activos/{id}/fotos/{fotoId}/archivo?miniatura=` | `Activos.Ver` | La imagen (o su miniatura) |
| `POST /api/activos/{id}/fotos` | `Activos.Editar` | Formulario con `archivo` (hasta 4 MB), `miniatura` (hasta 512 KB) y `descripcion` (opcional, 60 caracteres). Máximo 6 por activo; la primera queda como principal |
| `PUT /api/activos/{id}/fotos/{fotoId}` | `Activos.Editar` | Cambia la descripción |
| `POST /api/activos/{id}/fotos/{fotoId}/principal` | `Activos.Editar` | La marca como principal |
| `DELETE /api/activos/{id}/fotos/{fotoId}` | `Activos.Editar` | La quita con sus archivos; si era la principal, pasa a serlo la siguiente |

`GET /api/activos` trae `fotoPrincipalId` en cada activo (nulo si no tiene fotos).

## Paso 8-1: resumen del inicio (D48, D49)

Este paso **cambia el modelo**: agrega la columna `Grupo` a `catalogos.EstadosActivo`. Desde `back`, con la API detenida:

```powershell
dotnet ef migrations add GrupoEstado --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Al arrancar, la API asigna a los estados existentes el grupo que corresponde por su nombre (Operativo → `EnUso`,
Inoperativo → `FueraDeServicio`, De baja → `DeBaja`…) y lo anota en el registro.

| Endpoint | Permiso | Qué hace |
|---|---|---|
| `GET /api/resumen?sedeId=` | `Activos.Ver` | Cifras del inicio (§5.11). El superadministrador sin `sedeId` recibe además `redes` |
| `PUT /api/catalogos/estados-activo/{id}/grupo` | `Catalogos.GestionarGlobales` | `{ "grupo": "EnUso" }`: `EnUso`, `EnMantenimiento`, `FueraDeServicio`, `EnAlmacen` o `DeBaja` |
| `GET /api/activos?atencion=&falta=` | `Activos.Ver` | Filtros nuevos, sin los activos de baja (D49) |
| `GET /api/establecimientos?sinActivos=true` | Con sesión | Establecimientos sin ningún activo registrado |

`GET /api/catalogos/estados-activo` trae `grupo` en cada estado.

### Correo en producción

En el servidor se configura `Correo:Modo = Smtp` y los datos del SMTP (`Servidor`, `Puerto`, `Remitente`, `Usuario`,
`Contrasena`) como variables de entorno o en `appsettings.Production.json` (no versionado). También `Aplicacion:UrlFrontend`
con la dirección pública del sistema.

## Pruebas automáticas

Desde la carpeta `back`:

```powershell
dotnet test
```

- `global.json` activa **Microsoft Testing Platform**, que es la que usa xUnit v3 4.x con el SDK de .NET 10.
- Las **pruebas de integración** crean y borran la base **`InventarioTIC_Pruebas`** en `.\SQLEXPRESS` en cada ejecución.
  Tu base de desarrollo `InventarioTIC` no se toca. Si tu SQL Server tiene otro nombre, define antes la variable de entorno:
  `$env:INVENTARIO_PRUEBAS_SQL = "Server=...;Database=InventarioTIC_Pruebas;Trusted_Connection=True;TrustServerCertificate=True"`.
- Las pruebas usan sus propios usuarios y contraseñas (solo existen en la base de pruebas) y no leen tus *user secrets*.

| Proyecto | Qué comprueba |
|---|---|
| `Domain.Tests` | Códigos de sede, catálogo de permisos, roles del sistema e iniciales, reglas de asignación de roles, catálogos, establecimientos (datos válidos, ubigeo, reconocimiento de la red), activos (ficha, IP y MAC, discos y RAM, garantía y renovación), fotos (formato por el contenido, limpieza de EXIF, límites), grupo de cada estado por su nombre |
| `ArchitectureTests` | Que el dominio no dependa de nada, que Application no dependa de Infrastructure ni de la Api, que los handlers sean internos |
| `Api.IntegrationTests` | Protección CSRF (sin token → 400; el token de antes de iniciar sesión no sirve después). Sin sesión → 401; sin permiso → 403; contraseña temporal → 403; un usuario de Huanta no ve ni puede tocar datos de Puquio; nadie cambia la sede de un registro; una petición anónima no ve nada. Gestión (4B): nadie otorga permisos que no tiene, nadie gestiona a quien tiene más permisos, solo el superadmin gestiona administradores y sedes, desactivar y restablecer funcionan. Fotos (7D-1): sin EXIF al descargar, principal, máximo de 6, solo imágenes, permisos, otra red recibe 404, los archivos se borran con la foto y con el activo. Resumen (8-1): cifras exactas en una red nueva, los de baja fuera de las alertas, los filtros de la lista coinciden con el resumen, tabla por red solo para el superadministrador, solo él cambia el grupo de un estado |

Para ejecutar solo un proyecto: `dotnet test --project tests/Inventario.Domain.Tests`.

## Cambios en el modelo (migraciones siguientes)

Cada vez que cambie una entidad o su configuración:

```powershell
dotnet ef migrations add NombreDelCambio --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Para producción no se aplican migraciones al arrancar: se genera un script revisable con
`dotnet ef migrations script --idempotent ...` (docs/ARQUITECTURA.md §10).

## Decisiones de diseño

- **Usuario y credenciales separados (D19).** `Domain.Usuarios.Usuario` guarda los datos de negocio (sede, nombres, roles).
  `Infrastructure.Identidad.CuentaUsuario` guarda las credenciales de Identity (usuario, correo, contraseña, bloqueo).
  Comparten el mismo identificador. Así el dominio no depende de Identity.
- **Permisos definidos en código** (`Domain.Seguridad.Permisos`). Las sedes solo los combinan en roles.
- **Roles del sistema sin permisos guardados.** Superadministrador y Administrador de sede calculan sus permisos
  desde el catálogo, así que un permiso nuevo les llega sin migraciones.
- **Reglas en el dominio** (lanzan `DominioException`): un usuario tiene al menos un rol; nunca el de superadmin;
  solo roles de su sede; los roles del sistema no se editan; un rol de sede no puede tener permisos exclusivos.
  Las reglas que dependen de quién hace la acción están en Application (`Common/Seguridad/ReglasDeGestion.cs`).
- **Solo raíces de agregado en `IAppDbContext`**: los permisos de un rol se cambian a través del rol y los roles de un
  usuario a través del usuario.
- **Identificadores GUID v7**: únicos y ordenados por fecha, eficientes como clave en SQL Server.
