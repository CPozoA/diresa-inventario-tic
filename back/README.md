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
└─ src/
   ├─ Inventario.Domain/         entidades y reglas puras, sin dependencias
   │  ├─ Common/                 Entidad, EntidadAuditable, IPerteneceASede, DominioException
   │  ├─ Sedes/                  Sede, SedesIniciales (las 7 redes de salud)
   │  ├─ Seguridad/              Permisos (catálogo), Rol, RolPermiso, TipoRol, RolesDelSistema
   │  └─ Usuarios/               Usuario, UsuarioRol
   ├─ Inventario.Application/    casos de uso por funcionalidad
   │  └─ Common/                 Result, ErrorAplicacion, ICommand/IQuery y sus handlers, IUsuarioActual
   ├─ Inventario.Infrastructure/ persistencia, Identity, correo
   │  └─ Identidad/              CuentaUsuario (credenciales de Identity)
   └─ Inventario.Api/            endpoints (Minimal APIs), seguridad, errores
      └─ Endpoints/              /api/salud
```

Los proyectos de pruebas (`tests/`) se agregan en el Paso 4.

## Decisiones de diseño del Paso 1

- **Usuario y credenciales separados.** `Domain.Usuarios.Usuario` guarda los datos de negocio (sede, nombres, roles).
  `Infrastructure.Identidad.CuentaUsuario` guarda las credenciales de Identity (usuario, correo, contraseña, bloqueo).
  Comparten el mismo identificador. Así el dominio no depende de Identity.
- **Permisos definidos en código** (`Domain.Seguridad.Permisos`). Las sedes solo los combinan en roles.
- **Roles del sistema sin permisos guardados.** Superadministrador y Administrador de sede calculan sus permisos
  desde el catálogo, así que un permiso nuevo les llega sin migraciones.
- **Reglas en el dominio** (lanzan `DominioException`): un usuario tiene al menos un rol; nunca el de superadmin;
  solo roles de su sede; los roles del sistema no se editan; un rol de sede no puede tener permisos exclusivos.
  Las reglas que dependen de quién hace la acción (no otorgar permisos que uno no tiene, no cambiarse sus propios roles)
  irán en Application.
- **Identificadores GUID v7**: únicos y ordenados por fecha, eficientes como clave en SQL Server.

## Cómo compilar y ejecutar

Desde la carpeta `back`:

```powershell
dotnet build
dotnet run --project src/Inventario.Api
```

Luego abre <https://localhost:7180/api/salud>. Debe responder algo como:

```json
{ "estado": "ok", "entorno": "Development", "version": "1.0.0.0", "fechaServidor": "..." }
```

Si el navegador advierte por el certificado de desarrollo, ejecuta una vez `dotnet dev-certs https --trust`.
