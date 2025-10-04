namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para paginación de historial de backups.
    /// </summary>
    public class BackupHistoryPagedViewModel
    {
        public IEnumerable<Models.BackupHistory> Items { get; set; } = Array.Empty<Models.BackupHistory>();
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public string Search { get; set; } = string.Empty;
    }
}
