using BackupPro.Data;
using BackupPro.Filters;
using BackupPro.Services;
using BackupPro.Services.Backup;
using BackupPro.Services.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BackupPro
{
    public class Program
    {
        /// <summary>
        /// Punto de entrada principal de la aplicación BackupPro.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos.</param>
        public static void Main(string[] args)
        {
            // Crear el builder primero
            var builder = WebApplication.CreateBuilder(args);

            // Registrar configuraciones como Singleton.
            // Los valores por defecto viven en AppSettings, pero se sobrescriben con lo que venga
            // de appsettings.json / appsettings.{Environment}.json / variables de entorno (p.ej.
            // Smtp__Password, GoogleOAuth__ClientSecret, OneDrive__ClientSecret). Así ningún secreto
            // real queda hardcodeado en el código fuente. Ver appsettings.Example.json y el README.
            var appSettings = new AppSettings();
            builder.Configuration.Bind(appSettings);
            builder.Services.AddSingleton(appSettings);
            builder.Services.AddSingleton(appSettings.Smtp);
            builder.Services.AddSingleton(appSettings.GoogleOAuth);
            builder.Services.AddSingleton(appSettings.OneDrive);
            builder.Services.AddSingleton(appSettings.Scheduler);

            // Puerto HTTPS opcional (deshabilitado por defecto). Definir Kestrel:HttpsPort para
            // habilitarlo; requiere además configurar un certificado (ver README).
            var httpsPort = builder.Configuration.GetValue<int?>("Kestrel:HttpsPort");

            // Forzar Kestrel a escuchar en el puerto 5070 (HTTP)
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5070); // HTTP en el puerto 5070

                if (httpsPort.HasValue)
                {
                    options.ListenAnyIP(httpsPort.Value, listenOptions => listenOptions.UseHttps());
                }
            });

            // Ruta absoluta para SQLite en carpeta Data (relativa al ejecutable)
            // Usa el directorio donde está el ejecutable, no el directorio de trabajo actual
            string baseDirectory = AppContext.BaseDirectory;
            string dataFolder = Path.Combine(baseDirectory, "Data");

            // Crear carpeta Data si no existe
            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            string dbPath = Path.Combine(dataFolder, "backuppro.db");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Configurar Identity (sin UI, usando controladores MVC personalizados)
            builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Configurar autenticación externa (Google y Microsoft). Se registran solo si hay
            // credenciales configuradas: un esquema OAuth con ClientId vacío hace que ASP.NET Core
            // lance una excepción al validar sus opciones en TODAS las peticiones (no solo al usar
            // ese login), no solo cuando se intenta usar. Así la app funciona con normalidad sin
            // tener las integraciones de Google/Microsoft configuradas todavía.
            var authBuilder = builder.Services.AddAuthentication();

            if (!string.IsNullOrWhiteSpace(appSettings.GoogleOAuth.ClientId) && !string.IsNullOrWhiteSpace(appSettings.GoogleOAuth.ClientSecret))
            {
                authBuilder.AddGoogle(googleOptions =>
                {
                    googleOptions.ClientId = appSettings.GoogleOAuth.ClientId;
                    googleOptions.ClientSecret = appSettings.GoogleOAuth.ClientSecret;
                    googleOptions.CallbackPath = "/signin-google";
                });
            }

            if (!string.IsNullOrWhiteSpace(appSettings.OneDrive.ClientId) && !string.IsNullOrWhiteSpace(appSettings.OneDrive.ClientSecret))
            {
                authBuilder.AddMicrosoftAccount(microsoftOptions =>
                {
                    microsoftOptions.ClientId = appSettings.OneDrive.ClientId;
                    microsoftOptions.ClientSecret = appSettings.OneDrive.ClientSecret;
                    microsoftOptions.CallbackPath = "/signin-microsoft";
                });
            }

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
            });

            // El header debe coincidir con el que envía el JavaScript del frontend
            // (fetch a los endpoints [FromBody] de los controladores de Storage/DataBase).
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });

            // Agregar MVC y servicios
            builder.Services.AddScoped<RequireSetupCompleteFilter>();
            builder.Services.AddControllersWithViews(options =>
            {
                // Mientras el sistema esté "recién instalado" (ver SetupState), obliga a
                // completar el asistente de configuración inicial antes de dejar usar cualquier
                // otra parte de la app.
                options.Filters.AddService<RequireSetupCompleteFilter>();
            });
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddSingleton<CredentialProtector>();

            // Servicios de token OAuth compartidos entre los controladores (explorar carpetas sin
            // exponer el token real al cliente) y los providers de backup (subir un archivo).
            builder.Services.AddScoped<GoogleDriveTokenService>();
            builder.Services.AddScoped<OneDriveTokenService>();

            // Providers de backup: un IDatabaseBackupProvider por motor de base de datos soportado y
            // un IStorageProvider por destino de almacenamiento soportado. Agregar un motor o destino
            // nuevo es escribir la clase e inscribirla acá; nada más del código necesita cambiar
            // (ver BackupProviderRegistry y BackupExecutionService).
            builder.Services.AddScoped<IDatabaseBackupProvider, SqlServerBackupProvider>();
            builder.Services.AddScoped<IDatabaseBackupProvider, MySqlBackupProvider>();
            builder.Services.AddScoped<IDatabaseBackupProvider, PostgresBackupProvider>();
            builder.Services.AddScoped<IDatabaseBackupProvider, MongoDbBackupProvider>();

            builder.Services.AddScoped<IStorageProvider, LocalStorageProvider>();
            builder.Services.AddScoped<IStorageProvider, FtpStorageProvider>();
            builder.Services.AddScoped<IStorageProvider, BlobStorageProvider>();
            builder.Services.AddScoped<IStorageProvider, GoogleDriveStorageProvider>();
            builder.Services.AddScoped<IStorageProvider, OneDriveStorageProvider>();

            builder.Services.AddScoped<BackupProviderRegistry>();
            builder.Services.AddScoped<BackupExecutionService>();

            // Ejecuta las tareas programadas automáticamente cuando llega su hora (antes esto
            // requería que alguien entrara a la interfaz y apretara "Ejecutar" a mano).
            builder.Services.AddHostedService<BackupSchedulerBackgroundService>();

            // Agregar soporte para sesiones
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // Aplicar migraciones y crear BD si no existe
            AplicarMigraciones(app);

            // Generar (si hace falta) y cachear la clave maestra de cifrado apenas arranca la app,
            // en vez de esperar a que alguien guarde la primera credencial. Es un Singleton, así
            // que forzar su creación acá no cambia su comportamiento, solo el momento: el archivo
            // Data/master.key queda listo desde el primer arranque sin que el usuario tenga que
            // hacer ni ver nada al respecto.
            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<CredentialProtector>();
            }

            // Pipeline HTTP
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();

            if (httpsPort.HasValue)
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession(); // Habilitar sesiones

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

            // Abrir navegador automáticamente al iniciar
            AbrirNavegador("http://localhost:5070");

            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR FATAL: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Aplica migraciones pendientes y crea la base de datos si no existe.
        /// </summary>
        private static void AplicarMigraciones(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                Console.WriteLine("Aplicando migraciones a la base de datos...");
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error aplicando migraciones: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Abre el navegador automáticamente al iniciar la aplicación (solo en modo interactivo).
        /// </summary>
        private static void AbrirNavegador(string url)
        {
            try
            {
                if (Environment.UserInteractive)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo abrir el navegador: {ex.Message}");
            }
        } 
    }
}
