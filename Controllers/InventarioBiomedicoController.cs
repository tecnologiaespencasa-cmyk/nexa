using IntranetPrueba.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.InventarioBiomedico)]
public class InventarioBiomedicoController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
