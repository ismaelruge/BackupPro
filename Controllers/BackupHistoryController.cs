using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers
{
    public class BackupHistoryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
