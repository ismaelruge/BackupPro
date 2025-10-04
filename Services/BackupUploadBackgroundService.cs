//using BackupPro.Data;
//using BackupPro.Models;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.EntityFrameworkCore;
//using System.Collections.Concurrent;

//namespace BackupPro.Services
//{
//    /// <summary>
//    /// Servicio en segundo plano para subir archivos de backup a almacenamiento externo de forma asíncrona y escalable.
//    /// </summary>
//    public class BackupUploadBackgroundService : BackgroundService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<BackupUploadBackgroundService> _logger;
//        private static readonly ConcurrentQueue<(string backupPath, BackupHistory history)> _queue = new();

//        public BackupUploadBackgroundService(IServiceProvider serviceProvider, ILogger<BackupUploadBackgroundService> logger)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Encola un archivo de backup para ser subido en segundo plano.
//        /// </summary>
//        public static void Enqueue(string backupPath, BackupHistory history)
//        {
//            _queue.Enqueue((backupPath, history));
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                if (_queue.TryDequeue(out var item))
//                {
//                    try
//                    {
//                        using var scope = _serviceProvider.CreateScope();
//                        var storageService = scope.ServiceProvider.GetRequiredService<StorageService>();
//                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//                        var config = await context.CompanyConfigs.FirstOrDefaultAsync();
//                        if (config == null)
//                        {
//                            await Task.Delay(2000, stoppingToken);
//                            continue;
//                        }

//                        bool subidaExitosa = false;
//                        switch (config.StorageType)
//                        {
//                            case "FTP":
//                                if (!string.IsNullOrWhiteSpace(config.FtpHost) && !string.IsNullOrWhiteSpace(config.FtpUser) && !string.IsNullOrWhiteSpace(config.FtpPassword))
//                                {
//                                    await storageService.UploadToFtpAsync(config.FtpHost, config.FtpUser, config.FtpPassword, item.backupPath);
//                                    subidaExitosa = true;
//                                }
//                                break;
//                            case "GoogleDrive":
//                                await storageService.UploadToGoogleDriveAsync(config, item.backupPath);
//                                subidaExitosa = true;
//                                break;
//                            case "OneDrive":
//                                await storageService.UploadToOneDriveAsync(config, item.backupPath);
//                                subidaExitosa = true;
//                                break;
//                            case "BlobStorage":
//                                await storageService.UploadToAzureBlobAsync(config, item.backupPath);
//                                subidaExitosa = true;
//                                break;
//                            case null:
//                            case "":
//                                // Sin storage en nube configurado: sólo copia local si está habilitado
//                                subidaExitosa = true;
//                                break;
//                        }
//                        // Copia local si aplica
//                        if (config.KeepLocalCopy && !string.IsNullOrWhiteSpace(config.LocalBackupPath))
//                        {
//                            try
//                            {
//                                Directory.CreateDirectory(config.LocalBackupPath);
//                                var dest = Path.Combine(config.LocalBackupPath, Path.GetFileName(item.backupPath));
//                                File.Copy(item.backupPath, dest, overwrite: true);
//                                _logger.LogInformation("Copia local guardada en {Destino}", dest);
//                            }
//                            catch (Exception ex)
//                            {
//                                _logger.LogWarning(ex, "No se pudo guardar copia local en {Ruta}", config.LocalBackupPath);
//                            }
//                        }

//                        if (subidaExitosa && File.Exists(item.backupPath) && !config.KeepLocalCopy)
//                        {
//                            File.Delete(item.backupPath);
//                        }
//                        _logger.LogInformation("Backup subido correctamente a {StorageType}: {BackupPath}", config.StorageType, item.backupPath);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError(ex, "Error subiendo backup a almacenamiento externo en background");
//                    }
//                }
//                else
//                {
//                    await Task.Delay(2000, stoppingToken); // Espera si la cola está vacía
//                }
//            }
//        }
//    }
//}
