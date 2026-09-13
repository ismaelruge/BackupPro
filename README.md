# BackupPro

Aplicación web (ASP.NET Core 9 MVC) para programar y gestionar backups de bases de datos hacia
distintos destinos de almacenamiento.

- **Bases de datos soportadas:** SQL Server, MySQL, PostgreSQL, MongoDB.
- **Destinos de almacenamiento soportados:** disco local, FTP, Azure Blob Storage, Google Drive,
  OneDrive.
- **Programación de tareas:** ejecución manual o programada (por minutos/horas/días) desde la
  interfaz web, con historial de resultados.

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
`admin` / `admin` (solo desde el propio equipo, ver más abajo) para llegar al asistente de
configuración inicial (`SetupAdmin`), donde se crea el usuario administrador definitivo y se
elimina el usuario temporal.

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
| `BACKUPPRO_MASTER_KEY` | Clave maestra (256 bits en Base64) para cifrar credenciales guardadas en la BD. Generar con `openssl rand -base64 32`. Si no se define, la app genera y persiste una automáticamente en `Data/master.key` la primera vez que arranca — cómodo para desarrollo, pero en producción se recomienda fijarla explícitamente (así no depende de un archivo local y se puede rotar/recuperar de forma controlada). |
| `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSSL`, `Smtp__Email`, `Smtp__Password` | Credenciales SMTP para el envío de notificaciones y recuperación de contraseña. |
| `GoogleOAuth__ClientId`, `GoogleOAuth__ClientSecret` | Credenciales de la app OAuth de Google (para conectar Google Drive como destino). |
| `OneDrive__ClientId`, `OneDrive__ClientSecret` | Credenciales de la app OAuth de Microsoft/Azure AD (para conectar OneDrive como destino). |
| `Kestrel__HttpsPort` | Puerto HTTPS opcional (ver [HTTPS](#https-y-despliegue)). |

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

Si pierdes `BACKUPPRO_MASTER_KEY` (o se borra `Data/master.key` sin haberla fijado), las
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

- `Controllers/DataBasesTypes/*`: CRUD + prueba de conexión + generación de backup para cada motor
  de base de datos soportado.
- `Controllers/StorageTypes/*`: CRUD + subida de backups para cada destino de almacenamiento.
- `Controllers/TaskSchedulerController.cs`: programación y ejecución de tareas; despacha el backup
  correspondiente mediante un diccionario `(tipo de BD, tipo de almacenamiento) -> función`, en vez
  de una cadena larga de `if/else`.
- `Services/CredentialProtector.cs`: cifrado/descifrado de credenciales (ver arriba).
- `Services/EmailService.cs`: envío de notificaciones por correo.

Los controladores de BD y de storage comparten un mismo patrón CRUD (crear/editar/eliminar/obtener
+ operación de backup), duplicado entre los 4 tipos de BD y los 5 tipos de storage. Consolidarlo
detrás de interfaces (`IDatabaseBackupProvider`, `IStorageProvider`) es la siguiente mejora de
arquitectura pendiente, pero no se abordó en esta pasada por el alcance que implica tocar los 9
controladores y sus vistas a la vez.

## Licencia

Ver [LICENSE.txt](LICENSE.txt).
