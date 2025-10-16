using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.DataBasesTypes
{
    public class MySqlDataBaseController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
    }
}
