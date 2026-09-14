using BackupPro.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Data
{
    /// <summary>
    /// DbContext principal de la aplicación. Contiene Identity y entidades de dominio.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
           : base(options)
        {

        }

        public DbSet<CompanyConfig> CompanyConfigs { get; set; }
        public DbSet<BackupHistory> BackupHistories { get; set; }
        public DbSet<RestoreHistory> RestoreHistories { get; set; }

        // Storage Configurations
        public DbSet<GoogleDriveStorage> GoogleDriveStorages { get; set; }
        public DbSet<OneDriveStorage> OneDriveStorages { get; set; }
        public DbSet<AzureBlobStorage> AzureBlobStorages { get; set; }
        public DbSet<FtpStorage> FtpStorages { get; set; }
        public DbSet<LocalStorage> LocalStorages { get; set; }

        // Database Configurations
        public DbSet<SqlServerDataBase> SqlServerDataBases { get; set; }
        public DbSet<PostgresSqlDataBase> PostgresSqlDataBases { get; set; }
        public DbSet<MySqlDataBase> MySqlDataBases { get; set; }
        public DbSet<MongoDBDataBase> MongoDBDataBases { get; set; }
        public DbSet<SqliteDataBase> SqliteDataBases { get; set; }

        // Task Scheduler
        public DbSet<Models.TaskScheduler> TaskSchedulers { get; set; }
    }
}
