# BackupPro

Aplicación web (ASP.NET Core 9 MVC) para programar y gestionar backups de bases de datos hacia
distintos destinos de almacenamiento. **De uso gratuito**, con un requisito de atribución de
autoría (ver [Licencia](#licencia)).

- **Bases de datos soportadas:** SQL Server, MySQL, PostgreSQL, MongoDB.
- **Destinos de almacenamiento soportados:** disco local, FTP, Azure Blob Storage, Google Drive,
  OneDrive.
- **Programación de tareas:** ejecución manual desde la interfaz web, o automática (por
  minutos/horas/días) mediante un servicio en segundo plano que revisa las tareas vencidas cada
  minuto — no requiere que alguien entre a la app para que corran. Historial de resultados incluido.
- **Retención automática de backups ("3x6"):** ver [Retención de backups](#retención-de-backups).

## Requisitos

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- Para backups de SQL Server: el propio motor de SQL Server debe poder escribir en la carpeta
  temporal usada para generar el `.bak` (ver [Notas de seguridad](#notas-de-seguridad)).
- Para backups de MySQL/PostgreSQL/MongoDB: las utilidades de línea de comandos correspondientes
  deben estar instaladas y accesibles (`mysqldump`, `pg_dump`, `mongodump`).

## Cómo ejecutar en desarrollo

```bash
dotnet restore
dotnet run
```

La app crea automáticamente una base de datos SQLite (`Data/backuppro.db`, relativa al ejecutable)
y aplica las migraciones pendientes al iniciar. Por defecto escucha en `http://localhost:5070` y
abre el navegador automáticamente.

**Primer inicio:** si no existe ningún usuario, se puede iniciar sesión una sola vez con
`admin` / `admin` (solo desde el propio equipo, ver más abajo). Esto lleva directo al asistente de
configuración inicial (`/Account/SetupAdmin`, no al login) donde hay que crear el usuario
administrador definitivo (nombre, correo, contraseña) y los datos básicos de notificación (nombre
de la empresa, correos adicionales). **Es obligatorio completarlo**: mientras no se haga, no se
puede usar ninguna otra parte de la aplicación (cualquier otra pantalla rebota de vuelta al
asistente). Al terminar, el usuario `admin` temporal se elimina. El resto de la configuración
(SMTP, correos, contraseña) se puede editar después desde **Configuración de la Empresa**.

## Configuración y secretos

Ningún secreto real debe quedar escrito en el código fuente ni commiteado al repositorio. La
configuración se resuelve, en este orden, con lo primero que encuentre:

1. Variables de entorno (recomendado para producción).
2. `appsettings.{Environment}.json` / `appsettings.json` (usar solo en local, sin commitear
   secretos reales — `appsettings.Local.json` y `appsettings.Production.json` ya están en
   `.gitignore` por si se necesita un archivo con valores reales).
3. Los valores por defecto de `appsettings.cs` (vacíos para todo lo sensible).

`appsettings.Example.json` documenta la forma de cada sección; cópiala como base para tu
`appsettings.Development.json` local o para las variables de entorno equivalentes
(`Smtp__Password`, `GoogleOAuth__ClientSecret`, `OneDrive__ClientSecret`, etc. — el `__` separa
secciones anidadas, siguiendo la convención estándar de configuración de ASP.NET Core).

### Variables de entorno relevantes

| Variable | Para qué sirve |
|---|---|
| `BACKUPPRO_MASTER_KEY` | Clave maestra (256 bits en Base64) para cifrar credenciales guardadas en la BD. Generar con `openssl rand -base64 32`. Si no se define, la app la genera sola **apenas arranca** (no hace falta ninguna acción ni pantalla del usuario) y la guarda en su propia base de datos SQLite, `Data/keystore.db` — separada de `Data/backuppro.db`, donde viven las credenciales ya cifradas, para que copiar/filtrar una no exponga automáticamente la otra. Cómodo para desarrollo, pero en producción se recomienda fijarla explícitamente (así no depende de un archivo local y se puede rotar/recuperar de forma controlada). |
| `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSSL`, `Smtp__Email`, `Smtp__Password` | Credenciales SMTP para el envío de notificaciones y recuperación de contraseña. |
| `GoogleOAuth__ClientId`, `GoogleOAuth__ClientSecret` | Credenciales de la app OAuth de Google (para conectar Google Drive como destino). |
| `OneDrive__ClientId`, `OneDrive__ClientSecret` | Credenciales de la app OAuth de Microsoft/Azure AD (para conectar OneDrive como destino). |
| `Kestrel__HttpsPort` | Puerto HTTPS opcional (ver [Notas de seguridad](#notas-de-seguridad)). |

## Cifrado de credenciales

Las contraseñas de bases de datos, contraseñas FTP, connection strings de Azure y tokens OAuth de
Google/OneDrive se cifran antes de guardarse en la base de datos (`Services/CredentialProtector.cs`):

- La clave de cifrado se deriva con **Argon2id** (memory-hard, resistente a fuerza bruta) a partir
  de la clave maestra (`BACKUPPRO_MASTER_KEY`).
- El cifrado en sí es **AES-256-GCM** (autenticado), con sal y nonce distintos en cada valor
  cifrado — el mismo texto plano nunca produce el mismo resultado dos veces.
- Ninguno de estos endpoints (`GetById`, etc.) devuelve el secreto real al navegador: al editar una
  configuración, el campo de contraseña/connection string se muestra vacío ("dejar en blanco para
  no cambiar"). Para Google Drive/OneDrive, explorar carpetas de una configuración ya guardada usa
  endpoints del lado servidor (`.../ListXxxFoldersById`, `.../CreateXxxFolderById`) que descifran y
  refrescan el token internamente, sin exponerlo nunca al cliente.

Si pierdes `BACKUPPRO_MASTER_KEY` (o se borra `Data/keystore.db` sin haberla fijado), las
credenciales cifradas quedan irrecuperables y habrá que volver a introducirlas.

## Notas de seguridad

- **HTTPS:** por defecto la app solo escucha en HTTP (puerto 5070), pensado para uso en localhost.
  Para exponerla en una red, define `Kestrel__HttpsPort` y configura un certificado (por ejemplo
  con `Kestrel__Certificates__Default__Path` / `Kestrel__Certificates__Default__Password`), o mejor
  aún, ponla detrás de un reverse proxy (IIS, nginx, Caddy) que termine TLS.
- **Acceso `admin`/`admin` de primer arranque:** solo funciona cuando no hay ningún usuario creado
  y la petición viene del propio equipo (loopback); no es explotable de forma remota.
- **Permisos de carpetas:** la app ya no otorga permisos de `Everyone`/`BUILTIN\Users` sobre las
  carpetas de backup. Para SQL Server, que el propio motor pueda escribir la carpeta temporal
  (`EnsureFolderWritePermissions` en `SqlServerDataBaseController`) solo se conceden permisos a las
  cuentas de servicio de SQL Server, nunca a "todos los usuarios de la máquina".
- **Contraseñas de mysqldump/mongodump:** se pasan mediante un archivo de credenciales temporal (no
  como argumento de línea de comandos), para que no queden visibles en la lista de procesos del
  sistema; PostgreSQL ya usaba `PGPASSWORD` por variable de entorno.

## Arquitectura

El proyecto es un único proyecto ASP.NET Core MVC (no está separado en varias capas físicas/DLLs),
organizado en capas lógicas por carpeta:

- **Presentación** — `Controllers/` + `Views/` + `ViewModels/` + `wwwroot/`.
  - `Controllers/DataBasesTypes/*` y `Controllers/StorageTypes/*`: CRUD (crear/editar/eliminar/listar)
    de cada configuración de base de datos y de almacenamiento.
  - `Controllers/TaskSchedulerController.cs`: CRUD de tareas programadas y disparo de ejecución
    manual ("Ejecutar"/"Ejecutar todas"); delega la ejecución real en `BackupExecutionService`.
  - `Controllers/AccountController.cs`: login, logout y el asistente de configuración inicial
    (`SetupAdmin`, ver [Primer inicio](#cómo-ejecutar-en-desarrollo)).
- **Dominio** — `Models/` (entidades que EF Core mapea a tablas).
- **Persistencia** — `Data/ApplicationDbContext.cs` + `Migrations/` (EF Core + SQLite).
- **Servicios / lógica de negocio** — `Services/`:
  - `Services/Backup/IDatabaseBackupProvider.cs` / `IStorageProvider.cs`: una implementación por
    motor de base de datos (`SqlServerBackupProvider`, `MySqlBackupProvider`,
    `PostgresBackupProvider`, `MongoDbBackupProvider`) y por destino de almacenamiento
    (`LocalStorageProvider`, `FtpStorageProvider`, `BlobStorageProvider`,
    `GoogleDriveStorageProvider`, `OneDriveStorageProvider`). Agregar un motor o destino nuevo es
    escribir la clase correspondiente e inscribirla en `Program.cs`; no requiere tocar el resto de
    la aplicación.
  - `BackupProviderRegistry`: resuelve el provider correspondiente al tipo de una tarea.
  - `BackupExecutionService`: orquesta generar el backup y subirlo, usado tanto por el botón
    "Ejecutar" manual como por el servicio de ejecución automática.
  - `BackupSchedulerBackgroundService`: `BackgroundService` que revisa cada minuto las tareas
    activas vencidas (`NextRunAt <= ahora`) y las ejecuta automáticamente con
    `BackupExecutionService`, sin intervención manual.
  - `BackupFrequencyCalculator`: calcula la próxima fecha de ejecución según la frecuencia de la
    tarea (minutos/horas/días); compartido entre `TaskSchedulerController` y
    `BackupSchedulerBackgroundService`.
  - `ExecutableLocator` / `StorageProviderBase`: utilidades compartidas (buscar `mysqldump`/
    `pg_dump`/`mongodump` en el PATH, registrar el histórico de resultados, formatear tamaños).
  - `Services/OAuth/GoogleDriveTokenService.cs` / `OneDriveTokenService.cs`: refresco y validación
    de tokens OAuth, compartido entre los controladores (explorar carpetas) y los storage providers
    (subir un backup).
  - `CredentialProtector.cs`: cifrado/descifrado de credenciales (ver [arriba](#cifrado-de-credenciales)).
  - `EmailService.cs`: envío de notificaciones por correo.
  - `SetupState.cs`: detecta si el sistema sigue en el estado de "recién instalado" (el único
    usuario existente es el `admin` temporal); usado por `AccountController` y por
    `Filters/RequireSetupCompleteFilter.cs`, que bloquea el resto de la aplicación hasta que se
    complete el asistente de configuración inicial.
- **Composición** — `Program.cs` (registro de servicios en el contenedor de DI, pipeline HTTP).

Un matiz honesto: no es una arquitectura N-Tier estricta. Los 9 controladores de
`DataBasesTypes/`+`StorageTypes/` siguen accediendo a `ApplicationDbContext` directamente para su
propio CRUD, en vez de pasar por una capa de servicio — el refactor a providers se enfocó en la
lógica de *ejecutar* un backup (que sí estaba duplicada 20 veces), no en el CRUD de cada
configuración.

## Retención de backups

`BackupRetentionService` aplica automáticamente una política "3x6", una vez al día, sobre cada
combinación de base de datos + destino de almacenamiento por separado:

1. Busca backups exitosos de esa combinación con más de 6 meses de antigüedad. Si no hay ninguno,
   no hace nada.
2. Si los hay, cuenta cuántos backups de esa misma combinación tienen **menos** de 6 meses.
   - Si son **menos de 3**, no borra nada — se necesita ese mínimo de respaldo reciente antes de
     prescindir de los más viejos.
   - Si son **3 o más**, borra todos los backups de más de 6 meses de esa combinación: el archivo
     real en el destino (disco local, FTP, Azure Blob, Google Drive o OneDrive) y su fila en el
     histórico.

Si el borrado del archivo remoto falla (por ejemplo, no se pudo conectar al FTP), el registro se
conserva y se reintenta al día siguiente — nunca se borra el histórico sin haber confirmado que el
archivo real ya no existe.

**Limitación conocida:** los backups guardados con una versión anterior a esta función no tienen
guardado el destino de almacenamiento exacto en su fila del histórico, así que la política de
retención los ignora por completo (no se borran automáticamente; hay que hacerlo a mano si se
quiere). Solo los backups generados después de este cambio participan de la retención automática.

## Pruebas

```bash
dotnet test
```

`BackupPro.Tests/` (xUnit + Moq + EF Core InMemory) cubre la lógica de negocio en `Services/`:
`BackupFrequencyCalculator`, `BackupProviderRegistry`, `BackupExecutionService` (casos de éxito y de
error, con providers simulados), `BackupRetentionService` (los distintos escenarios de la política
"3x6": nada que borrar, mínimo no cubierto, borrado exitoso, fallo al borrar el archivo, backups sin
destino registrado, series independientes), `CredentialProtector` (cifrado/descifrado, valores
legados sin cifrar) y `SetupState` (detección del estado "recién instalado"). No hay pruebas de
controladores ni de vistas todavía.

## Autor

Creado por **[Ismael Ruge González](https://ismaelruge.github.io)**.

## Licencia

Ver [LICENSE.txt](LICENSE.txt) — MIT con un requisito adicional de atribución y autoría. El
software es **gratuito para sus usuarios finales**, pero eso no renuncia a la autoría ni a la
propiedad intelectual del proyecto, que son de Ismael Ruge González. Toda copia, fork,
modificación o instancia pública de este software (modificada o no) debe:

- Mantener visible para sus usuarios finales, sin alterar, el aviso "Desarrollado por Ismael Ruge
  González" con enlace a [ismaelruge.github.io](https://ismaelruge.github.io), tal como aparece en
  el pie de página de la aplicación (`Views/Shared/_Layout.cshtml`).
- No quitar, alterar ni reasignar a otro nombre el autor, el aviso de copyright de
  [LICENSE.txt](LICENSE.txt), ni ningún otro aviso de autoría o propiedad intelectual del proyecto
  (incluyendo los metadatos de `BackupPro.csproj` y los créditos de este README).

Quitar, ocultar, alterar o reasignar cualquiera de esos avisos excede el permiso otorgado por la
licencia y constituye una infracción de los derechos de autor y propiedad intelectual, pudiendo
dar lugar a acciones legales — el texto completo y vinculante está en [LICENSE.txt](LICENSE.txt).
