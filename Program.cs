using BackupPro.Data;
using BackupPro.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
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
            // Manejar excepciones no controladas
            //AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            //{
            //    var exception = eventArgs.ExceptionObject as Exception;
            //    Console.WriteLine($"EXCEPCIÓN NO CONTROLADA: {exception?.Message}");
            //    Console.WriteLine($"Stack Trace: {exception?.StackTrace}");
            //    Log.Fatal(exception, "Excepción no controlada que causó el cierre de la aplicación");
            //};

            //TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            //{
            //    Console.WriteLine($"EXCEPCIÓN DE TAREA NO OBSERVADA: {eventArgs.Exception.Message}");
            //    Log.Error(eventArgs.Exception, "Excepción de tarea no observada");
            //    eventArgs.SetObserved();
            //};

            // Crear el builder primero
            var builder = WebApplication.CreateBuilder(args);

            // Registrar configuraciones como Singleton
            var appSettings = new AppSettings();
            builder.Services.AddSingleton(appSettings);
            builder.Services.AddSingleton(appSettings.Smtp);
            builder.Services.AddSingleton(appSettings.GoogleOAuth);
            builder.Services.AddSingleton(appSettings.OneDrive);
            builder.Services.AddSingleton(appSettings.Scheduler);

            // Configurar Serilog como logger principal
            //Log.Logger = new LoggerConfiguration()
            //    .ReadFrom.Configuration(builder.Configuration)
            //    .Enrich.FromLogContext()
            //    .CreateLogger();

            //builder.Host.UseSerilog();

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
            //builder.Services.AddScoped<BackupService>();
            builder.Services.AddScoped<EmailService>();
            //builder.Services.AddScoped<StorageService>();
            //builder.Services.AddHostedService<BackupUploadBackgroundService>();
            //builder.Services.AddHostedService<BackupPeriodicService>();

            // Registrar controladores para inyección de dependencias
            builder.Services.AddScoped<BackupPro.Controllers.DataBasesTypes.SqlServerDataBaseController>();
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
            //AplicarMigraciones(app);

            // NO crear usuario por defecto automáticamente
            // El login permitirá admin/admin solo si no hay usuarios

            //var backupPath = Path.Combine(app.Environment.ContentRootPath, "Backups");
            //if (!Directory.Exists(backupPath)) Directory.CreateDirectory(backupPath);

            // Limitar acceso a /Backups solo a usuarios autenticados
            //app.UseWhen(context => context.Request.Path.StartsWithSegments("/Backups"), appBuilder =>
            //{
            //    appBuilder.UseAuthentication();
            //    appBuilder.UseAuthorization();
            //});

            //app.UseStaticFiles(new StaticFileOptions
            //{
            //    FileProvider = new PhysicalFileProvider(backupPath),
            //    RequestPath = "/Backups"
            //});

            // Intentar crear tarea programada del sistema si está habilitado por configuración
            //TryCreateSystemScheduler(app);

            // Pipeline HTTP
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();

            app.UseStaticFiles();
            // No llamar a UseHttpsRedirection si no hay endpoint HTTPS configurado

            app.UseRouting();

            app.UseSession(); // Habilitar sesiones

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

            // Abrir navegador automáticamente (si se ejecuta manualmente)
            // TEMPORALMENTE DESHABILITADO PARA DEBUG
            AbrirNavegador("http://localhost:5070");

            try
            {
                //Log.Information("Iniciando aplicación BackupPro...");
                app.Run();
                //Log.Information("Aplicación BackupPro finalizada normalmente.");
            }
            catch (Exception ex)
            {
                //Log.Fatal(ex, "La aplicación se detuvo debido a una excepción");
                Console.WriteLine($"ERROR FATAL: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
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
                context.Database.Migrate();
                AjustarEsquemaSqlite(context);
            }
            catch (Exception ex)
            {
                //var logger = services.GetRequiredService<ILogger<Program>>();
                //logger.LogError(ex, "Error aplicando migraciones de la base de datos");
                throw;
            }
        }

        private static void AjustarEsquemaSqlite(ApplicationDbContext context)
        {
            try
            {
                using var conn = context.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('CompanyConfigs')";
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cols.Add(reader.GetString(1)); // name
                    }
                }
                using var alter = conn.CreateCommand();
                if (!cols.Contains("KeepLocalCopy"))
                {
                    alter.CommandText = "ALTER TABLE CompanyConfigs ADD COLUMN KeepLocalCopy INTEGER NOT NULL DEFAULT 0";
                    alter.ExecuteNonQuery();
                }
                if (!cols.Contains("LocalBackupPath"))
                {
                    alter.CommandText = "ALTER TABLE CompanyConfigs ADD COLUMN LocalBackupPath TEXT NULL";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Registrar pero no bloquear arranque si falla ajuste no crítico
                Console.WriteLine($"Ajuste de esquema SQLite falló: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea el usuario admin por defecto (admin/admin) si no existe ningún usuario.
        /// </summary>
        private static void CrearUsuarioAdminPorDefecto(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            //var logger = services.GetRequiredService<ILogger<Program>>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

            try
            {
                var hayUsuarios = userManager.Users.Any();
                if (!hayUsuarios)
                {
                    var user = new IdentityUser
                    {
                        UserName = "admin",
                        Email = "admin@local",
                        EmailConfirmed = true
                    };

                    // Para el usuario admin, crear directamente en la base de datos sin validaciones
                    user.PasswordHash = userManager.PasswordHasher.HashPassword(user, "admin");
                    
                    // Crear usuario directamente sin validaciones de contraseña
                    var result = userManager.CreateAsync(user).GetAwaiter().GetResult();
                    if (result.Succeeded)
                    {
                        //logger.LogInformation("Usuario por defecto creado: admin/admin");
                    }
                    else
                    {
                        //logger.LogError("Error creando usuario por defecto: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    //logger.LogInformation("La base de usuarios no está vacía. No se crea usuario por defecto.");
                }
            }
            catch (Exception ex)
            {
                //logger.LogError(ex, "Excepción creando usuario por defecto");
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

        /// <summary>
        /// Crea la tarea programada en Windows para ejecutar backups cada hora usando PowerShell y schtasks.
        /// (Opcional) — deshabilitada por defecto.
        /// </summary>
        private static void CrearTareaProgramada()
        {
            string taskName = "BackupPro-Backups-Automaticos";
            string dllPath = @"D:\\IsmaelRuge\\Documents\\UniRemington\\ProyectoGrado\\IIS\\BackupPro.dll";
            string url = "http://localhost:5070/api/scheduler/run";

            var checkTask = new ProcessStartInfo("schtasks", $"/Query /TN \"{taskName}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(checkTask))
            {
                string output = process!.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"La tarea '{taskName}' ya existe.");
                    return;
                }
                else
                {
                    Console.WriteLine($"La tarea '{taskName}' no existe. Creando nueva...");
                }
            }

            string psScriptPath = Path.Combine(Path.GetTempPath(), "BackupProScheduler.ps1");
            File.WriteAllText(psScriptPath, $@"
# Iniciar el DLL con dotnet
$process = Start-Process -FilePath 'dotnet' -ArgumentList '{dllPath}' -PassThru

# Esperar 15 segundos para que el servicio arranque
Start-Sleep -Seconds 15

# Ejecutar curl y esperar respuesta
$response = Invoke-WebRequest -Uri '{url}' -Method POST -TimeoutSec 120

# Si responde bien, cerrar el proceso
if ($response.StatusCode -eq 200) {{
    Stop-Process -Id $process.Id
}} else {{
    Write-Host 'Backup no respondió correctamente'
}}
");

            string cmd = $"/Create /SC HOURLY /TN \"{taskName}\" " +
             $"/TR \"powershell -ExecutionPolicy Bypass -File '{psScriptPath}'\" /F";

            var createTask = new ProcessStartInfo("schtasks", cmd)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(createTask))
            {
                string output = proc!.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Console.WriteLine(output);
                if (proc.ExitCode != 0)
                {
                    Console.WriteLine($"Error creando tarea: {error}");
                }
                else
                {
                    Console.WriteLine($"Tarea '{taskName}' creada correctamente en la raíz.");
                }
            }
        }

        /// <summary>
        /// Si Scheduler:UseSystemScheduler=true, intenta crear una tarea programada según el SO.
        /// Siempre continúa con el scheduler interno como fallback.
        /// </summary>
        private static void TryCreateSystemScheduler(WebApplication app)
        {
            //var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var schedulerSettings = app.Services.GetRequiredService<SchedulerSettings>();
            var useSystem = schedulerSettings.UseSystemScheduler;
            var interval = schedulerSettings.IntervalMinutes;
            var token = schedulerSettings.SecureToken;
            if (!useSystem)
            {
                //logger.LogInformation("Scheduler del sistema deshabilitado; se usará el scheduler interno.");
                return;
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                //logger.LogWarning("No se pudo crear tarea del sistema: Scheduler:SecureToken no configurado.");
                return;
            }

            var url = "http://localhost:5070/api/scheduler/run?token=" + Uri.EscapeDataString(token);
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    string taskName = "BackupPro-AutoBackups";
                    // Verificar si ya existe
                    var check = new ProcessStartInfo("schtasks", $"/Query /TN \"{taskName}\"")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(check)!;
                    proc.WaitForExit();
                    if (proc.ExitCode == 0)
                    {
                        //logger.LogInformation("La tarea de Windows '{Task}' ya existe.", taskName);
                        return;
                    }
                    // Crear script temporal que invoque el endpoint
                    string psPath = Path.Combine(Path.GetTempPath(), "BackupPro_Auto.ps1");
                    File.WriteAllText(psPath, $"try {{ Invoke-WebRequest -Uri '{url}' -Method POST -UseBasicParsing }} catch {{ }}");
                    string cmd = $"/Create /SC MINUTE /MO {Math.Max(5, interval)} /TN \"{taskName}\" " +
                                 $"/TR \"powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"\"{psPath}\"\"\" /F";
                    var create = new ProcessStartInfo("schtasks", cmd)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(create)!;
                    var outp = p.StandardOutput.ReadToEnd();
                    var err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    //if (p.ExitCode == 0)
                    //    logger.LogInformation("Tarea de Windows creada: {Msg}", outp.Trim());
                    //else
                    //    logger.LogWarning("No se pudo crear tarea de Windows: {Err}", err.Trim());
                }
                else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    // Intentar añadir una entrada al crontab del usuario
                    // Ejemplo: */60 * * * * curl -s -X POST "http://localhost:5070/api/scheduler/run?token=..."
                    var cronLine = $"*/{Math.Max(5, interval)} * * * * curl -s -X POST '{url}' >/dev/null 2>&1";
                    var exportCron = new ProcessStartInfo("/bin/sh", "-c \"(crontab -l 2>/dev/null; echo '" + cronLine.Replace("'", "'\\''") + "') | crontab -\"")
                    {
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(exportCron)!;
                    p.WaitForExit();
                    //if (p.ExitCode == 0)
                        //logger.LogInformation("Entrada de crontab añadida para ejecutar backups automáticos.");
                    //else
                        //logger.LogWarning("No se pudo añadir entrada a crontab: {Err}", p.StandardError.ReadToEnd().Trim());
                }
            }
            catch (Exception ex)
            {
                //logger.LogWarning(ex, "Fallo creando tarea programada del sistema. Se usará el scheduler interno.");
            }
        }
    }
}
