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
        public DbSet<DatabaseSource> DatabaseSources { get; set; }
        public DbSet<BackupHistory> BackupHistories { get; set; }
    }
}
