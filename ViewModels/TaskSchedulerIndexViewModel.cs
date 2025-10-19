namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para la vista Index de TaskScheduler
    /// Incluye información completa de la tarea con nombres de las configuraciones
    /// </summary>
    public class TaskSchedulerIndexViewModel
    {
        public int Id { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string DatabaseType { get; set; } = string.Empty;
        public int DatabaseId { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public int StorageId { get; set; }
        public string StorageName { get; set; } = string.Empty;
        public string FrequencyType { get; set; } = string.Empty;
        public int FrequencyValue { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
