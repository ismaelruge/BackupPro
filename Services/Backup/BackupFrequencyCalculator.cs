namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Calcula la próxima fecha de ejecución de una tarea programada según su frecuencia.
    /// Usado tanto por <see cref="Controllers.TaskSchedulerController"/> (al crear/editar una tarea
    /// o ejecutarla a mano) como por <see cref="BackupSchedulerBackgroundService"/> (al ejecutarla
    /// automáticamente), que antes tenían cada uno su propia copia de esta misma lógica.
    /// </summary>
    public static class BackupFrequencyCalculator
    {
        public static DateTime CalculateNextRun(string frequencyType, int frequencyValue, DateTime baseTime)
        {
            return frequencyType.ToLowerInvariant() switch
            {
                "minutes" => baseTime.AddMinutes(frequencyValue),
                "hours" => baseTime.AddHours(frequencyValue),
                "days" => baseTime.AddDays(frequencyValue),
                _ => baseTime.AddHours(1)
            };
        }
    }
}
