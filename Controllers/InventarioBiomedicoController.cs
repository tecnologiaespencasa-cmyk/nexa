using Nexa.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexa.Controllers;

[Authorize(Policy = SystemPermissions.InventarioBiomedico)]
public class InventarioBiomedicoController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
