using BackupPro.Data;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services
{
    /// <summary>
    /// Ejecuta las tareas programadas automáticamente cuando llega su hora. Antes de este servicio,
    /// "programar" un backup solo calculaba una fecha de próxima ejecución (NextRunAt) que nadie
    /// disparaba: la única forma de correr un backup era entrar a la interfaz y apretar "Ejecutar" a
    /// mano. Este servicio revisa cada minuto qué tareas activas están vencidas y las ejecuta usando
    /// el mismo <see cref="BackupExecutionService"/> que usa el botón "Ejecutar" manual.
    /// </summary>
    public class BackupSchedulerBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackupSchedulerBackgroundService> _logger;

        public BackupSchedulerBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BackupSchedulerBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Programador de backups automáticos iniciado (revisa tareas vencidas cada {Minutes} minuto(s)).", PollInterval.TotalMinutes);

            using var timer = new PeriodicTimer(PollInterval);

            try
            {
                do
                {
                    await RunDueTasksAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                // Apagado normal de la aplicación
            }
        }

        private async Task RunDueTasksAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var executionService = scope.ServiceProvider.GetRequiredService<BackupExecutionService>();

            var now = DateTime.Now;
            var dueTasks = await context.TaskSchedulers
                .Where(t => t.IsActive && t.NextRunAt <= now)
                .ToListAsync(stoppingToken);

            if (dueTasks.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Ejecutando {Count} tarea(s) de backup programada(s) vencida(s).", dueTasks.Count);

            foreach (var task in dueTasks)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    string result = await executionService.ExecuteAsync(task);
                    _logger.LogInformation("Backup programado '{TaskName}' completado: {Result}", task.TaskName, result);
                }
                catch (Exception ex)
                {
                    // Una tarea fallida no debe detener el ciclo ni las demás tareas: el error ya
                    // quedó registrado en el histórico por el provider correspondiente.
                    _logger.LogError(ex, "Error al ejecutar el backup programado '{TaskName}' ({TaskId})", task.TaskName, task.Id);
                }
                finally
                {
                    task.LastRunAt = now;
                    task.NextRunAt = BackupFrequencyCalculator.CalculateNextRun(task.FrequencyType, task.FrequencyValue, now);
                    task.LastModifiedAt = now;
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
