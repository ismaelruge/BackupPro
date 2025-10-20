using BackupPro.Data;
using BackupPro.Services;
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

            // Registrar configuraciones como Singleton
            var appSettings = new AppSettings();
            builder.Services.AddSingleton(appSettings);
            builder.Services.AddSingleton(appSettings.Smtp);
            builder.Services.AddSingleton(appSettings.GoogleOAuth);
            builder.Services.AddSingleton(appSettings.OneDrive);
            builder.Services.AddSingleton(appSettings.Scheduler);

            // Forzar Kestrel a escuchar en el puerto 5070 (HTTP)
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5070); // HTTP en el puerto 5070
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

            // Configurar Identity
            builder.Services.AddDefaultIdentity<IdentityUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
            });

            // Agregar MVC y servicios
            builder.Services.AddControllersWithViews();
            builder.Services.AddScoped<EmailService>();

            // Registrar controladores para inyección de dependencias
            builder.Services.AddScoped<BackupPro.Controllers.DataBasesTypes.SqlServerDataBaseController>();
            builder.Services.AddScoped<BackupPro.Controllers.DataBasesTypes.MySqlDataBaseController>();
            builder.Services.AddScoped<BackupPro.Controllers.DataBasesTypes.PostgresSqlDataBaseController>();
            builder.Services.AddScoped<BackupPro.Controllers.DataBasesTypes.MongoDBDataBaseController>();
            builder.Services.AddScoped<BackupPro.Controllers.StorageTypes.LocalStorageController>();
            builder.Services.AddScoped<BackupPro.Controllers.StorageTypes.FtpStorageController>();
            builder.Services.AddScoped<BackupPro.Controllers.StorageTypes.BlobStorageController>();
            builder.Services.AddScoped<BackupPro.Controllers.StorageTypes.OneDriveStorageController>();
            builder.Services.AddScoped<BackupPro.Controllers.StorageTypes.GoogleDriveStorageController>();

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

            // Pipeline HTTP
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();

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
