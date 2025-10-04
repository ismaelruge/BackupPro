using BackupPro.Data;
using BackupPro.Models;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
using Npgsql;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador para la gestión de orígenes de bases de datos a respaldar.
    /// </summary>
    [Authorize]
    public class DatabaseSourceController : Controller
    {
        #region Campos
        private readonly ApplicationDbContext _context;
        #endregion

        #region Constructor
        /// <summary>
        /// Inicializa una nueva instancia del <see cref="DatabaseSourceController"/>.
        /// </summary>
        /// <param name="context">Contexto de base de datos.</param>
        public DatabaseSourceController(ApplicationDbContext context)
        {
            _context = context;
        }
        #endregion

        #region Métodos CRUD
        /// <summary>
        /// Lista todos los orígenes de bases de datos registrados.
        /// </summary>
        /// <returns>Vista con la lista de orígenes.</returns>
        public async Task<IActionResult> Index()
        {
            var list = await _context.DatabaseSources.ToListAsync();
            return View(list);
        }

        /// <summary>
        /// Muestra el formulario para crear un nuevo origen de base de datos.
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new DatabaseSource());
        }

        /// <summary>
        /// Procesa la creación de un nuevo origen de base de datos.
        /// </summary>
        /// <param name="model">Datos del origen a crear.</param>
        /// <returns>Redirige a la lista si es exitoso, o retorna la vista con errores.</returns>
        [HttpPost]
        public async Task<IActionResult> Create(DatabaseSource model)
        {
            if (ModelState.IsValid)
            {
                // Verificar si ya existe una base de datos con el mismo nombre y tipo
                bool exists = await _context.DatabaseSources.AnyAsync(db => db.Name == model.Name && db.Type == model.Type);

                if (exists)
                {
                    // Mostrar error al usuario
                    ModelState.AddModelError(string.Empty, "Esta base de datos ya está registrada.");
                    return View(model);
                }

                _context.DatabaseSources.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        /// <summary>
        /// Muestra el formulario para editar un origen de base de datos.
        /// </summary>
        /// <param name="id">Id del origen a editar.</param>
        /// <returns>Vista de edición o NotFound si no existe.</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var db = await _context.DatabaseSources.FindAsync(id);
            if (db == null) return NotFound();
            return View(db);
        }

        /// <summary>
        /// Procesa la edición de un origen de base de datos.
        /// </summary>
        /// <param name="model">Datos editados del origen.</param>
        /// <returns>Redirige a la lista si es exitoso, o retorna la vista con errores.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit(DatabaseSource model)
        {
            if (ModelState.IsValid)
            {
                _context.DatabaseSources.Update(model);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        /// <summary>
        /// Elimina un origen de base de datos por su Id.
        /// </summary>
        /// <param name="id">Id del origen a eliminar.</param>
        /// <returns>Redirige a la lista tras eliminar.</returns>
        public async Task<IActionResult> Delete(int id)
        {
            var db = await _context.DatabaseSources.FindAsync(id);
            if (db != null)
            {
                _context.DatabaseSources.Remove(db);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }
        #endregion

        #region Métodos auxiliares
        /// <summary>
        /// Lista las bases de datos disponibles en el servidor especificado, según el tipo.
        /// </summary>
        /// <param name="type">Tipo de base de datos (SQLServer, PostgreSQL, MySQL, MongoDB).</param>
        /// <param name="server">Servidor o instancia.</param>
        /// <param name="user">Usuario de conexión.</param>
        /// <param name="password">Contraseña de conexión.</param>
        /// <param name="windowsAuth">Si se usa autenticación de Windows (solo SQL Server).</param>
        /// <returns>Lista de nombres de bases de datos en formato JSON.</returns>
        [HttpGet]
        public async Task<IActionResult> ListDatabases(string type, string server, string user, string password, bool windowsAuth)
        {
            try
            {
                List<string> databases = new();

                if (type == "SQLServer")
                {
                    // Primera conexión con cifrado
                    string connectionString = windowsAuth
                        ? $"Server={server};Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
                        : $"Server={server};User Id={user};Password={password};Encrypt=True;TrustServerCertificate=True;";

                    try
                    {
                        using var conn = new SqlConnection(connectionString);
                        await conn.OpenAsync();

                        using var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE database_id > 4", conn);
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            databases.Add(reader.GetString(0));
                        }
                    }
                    catch (SqlException ex)
                    {
                        // Si falla por SSL o certificados, intentamos sin cifrado
                        if (ex.Message.Contains("certificación") || ex.Message.Contains("SSL") || ex.Message.Contains("Trust"))
                        {
                            connectionString = windowsAuth
                                ? $"Server={server};Integrated Security=True;Encrypt=False;"
                                : $"Server={server};User Id={user};Password={password};Encrypt=False;";

                            using var conn2 = new SqlConnection(connectionString);
                            await conn2.OpenAsync();

                            using var cmd2 = new SqlCommand("SELECT name FROM sys.databases WHERE database_id > 4", conn2);
                            using var reader2 = await cmd2.ExecuteReaderAsync();
                            while (await reader2.ReadAsync())
                            {
                                databases.Add(reader2.GetString(0));
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else if (type == "PostgreSQL")
                {
                    string connectionString = $"Host={server};Username={user};Password={password};Database=postgres";

                    await using var conn = new NpgsqlConnection(connectionString);
                    await conn.OpenAsync();

                    await using var cmd = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datistemplate = false;", conn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
                else if (type == "MySQL")
                {
                    string connectionString = $"Server={server};User ID={user};Password={password};";

                    await using var conn = new MySqlConnection(connectionString);
                    await conn.OpenAsync();

                    await using var cmd = new MySqlCommand("SHOW DATABASES;", conn);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
                else if (type == "MongoDB")
                {
                    // Construcción de cadena de conexión Mongo
                    string connectionString = string.IsNullOrWhiteSpace(user)
                        ? $"mongodb://{server}"
                        : $"mongodb://{user}:{password}@{server}";

                    var client = new MongoClient(connectionString);
                    var dbs = await client.ListDatabaseNamesAsync();
                    databases = dbs.ToList();
                }

                return Json(databases);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }
        #endregion
    }
}
