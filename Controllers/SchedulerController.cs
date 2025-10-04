//using BackupPro.Services;
//using Microsoft.AspNetCore.Mvc;

//namespace BackupPro.Controllers
//{
//    /// <summary>
//    /// API para la ejecución programada de backups automáticos.
//    /// </summary>
//    [ApiController]
//    [Route("api/[controller]")]

//    public class SchedulerController : Controller
//    {
//        private readonly BackupService _schedulerService;
//        private readonly IConfiguration _config;

//        /// <summary>
//        /// Inicializa una nueva instancia del <see cref="SchedulerController"/>.
//        /// </summary>
//        /// <param name="schedulerService">Servicio de backups.</param>
//        /// <param name="config">Configuración de la aplicación.</param>
//        public SchedulerController(BackupService schedulerService, IConfiguration config)
//        {
//            _schedulerService = schedulerService;
//            _config = config;
//        }

//        /// <summary>
//        /// Endpoint para ejecutar backups automáticos. Requiere un token de seguridad.
//        /// </summary>
//        /// <param name="token">Token de seguridad para autorizar la ejecución.</param>
//        /// <returns>Resultado de la ejecución.</returns>
//        [HttpPost("run")]
//        public async Task<IActionResult> Run([FromQuery] string token)
//        {
//            var secureToken = _config["Scheduler:SecureToken"];
//            if (token != secureToken)
//            {
//                return Unauthorized("Token inválido");
//            }
//            await _schedulerService.EjecutarBackupsAutomatizadosAsync();
//            return Ok("Backups automáticos ejecutados correctamente");
//        }
//    }
//}
