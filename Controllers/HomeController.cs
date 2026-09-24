using System.Diagnostics;
using Nexa.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexa.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // El código que ve el usuario es el identificador de la operación: con él se encuentra el
        // error en Application Insights (operation_Id) y en el registro de la aplicación.
        var codigo = Activity.Current?.TraceId.ToString();
        return View(new ErrorViewModel { RequestId = string.IsNullOrEmpty(codigo) ? HttpContext.TraceIdentifier : codigo });
    }
}
