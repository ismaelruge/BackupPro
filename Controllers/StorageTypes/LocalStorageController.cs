using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class LocalStorageController : Controller
    {
        private readonly IConfiguration _configuration;

        public LocalStorageController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
