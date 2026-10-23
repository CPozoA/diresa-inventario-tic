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
   │  ├─ Sedes/                  Sede, SedesIniciales (las 7 redes de salud)
   │  ├─ Seguridad/              Permisos (catálogo), Rol, RolPermiso, TipoRol, RolesDelSistema
   │  └─ Usuarios/               Usuario, UsuarioRol
   ├─ Inventario.Application/    casos de uso por funcionalidad
   │  ├─ Common/                 Result, ErrorAplicacion, ICommand/IQuery, IUsuarioActual, IAppDbContext,
   │  │                          IServicioIdentidad, IServicioCorreo, IEnlacesAplicacion,
   │  │                          ReglasDeGestion (anti-escalamiento), GeneradorContrasenaTemporal
   │  ├─ Cuenta/                 iniciar/cerrar sesión, sesión actual, cambiar y recuperar contraseña
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
      └─ Seguridad/              cookie de sesión, permisos por endpoint, límite de intentos, cambio obligatorio de contraseña
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
| `Domain.Tests` | Códigos de sede, catálogo de permisos, roles del sistema e iniciales, reglas de asignación de roles |
| `ArchitectureTests` | Que el dominio no dependa de nada, que Application no dependa de Infrastructure ni de la Api, que los handlers sean internos |
| `Api.IntegrationTests` | Sin sesión → 401; sin permiso → 403; contraseña temporal → 403; un usuario de Huanta no ve ni puede tocar datos de Puquio; nadie cambia la sede de un registro; una petición anónima no ve nada. Gestión (4B): nadie otorga permisos que no tiene, nadie gestiona a quien tiene más permisos, solo el superadmin gestiona administradores y sedes, desactivar y restablecer funcionan |

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
