//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//namespace BackupPro.Services
//{
//    /// <summary>
//    /// Scheduler interno por tiempo fijo que invoca EjecutarBackupsAutomatizadosAsync periódicamente.
//    /// Funciona en cualquier SO como fallback o complemento del scheduler del sistema.
//    /// </summary>
//    public class BackupPeriodicService : BackgroundService
//    {
//        private readonly ILogger<BackupPeriodicService> _logger;
//        private readonly IServiceProvider _sp;
//        private readonly IConfiguration _cfg;

//        public BackupPeriodicService(ILogger<BackupPeriodicService> logger, IServiceProvider sp, IConfiguration cfg)
//        {
//            _logger = logger;
//            _sp = sp;
//            _cfg = cfg;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            var minutes = Math.Max(5, _cfg.GetValue("Scheduler:IntervalMinutes", 60));
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    using var scope = _sp.CreateScope();
//                    var backup = scope.ServiceProvider.GetRequiredService<BackupService>();
//                    await backup.EjecutarBackupsAutomatizadosAsync();
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Error ejecutando backups automatizados en scheduler interno");
//                }
//                await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
//            }
//        }
//    }
//}

