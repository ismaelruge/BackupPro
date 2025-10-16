using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.DataBasesTypes
{
    public class PostgresSqlDataBaseController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
    }
}
