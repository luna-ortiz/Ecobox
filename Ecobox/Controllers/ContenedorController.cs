using Ecobox.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecobox.Controllers;

public class ContenedorController(IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await dashboardService.GetDashboardAsync(cancellationToken);
        return View(dashboard);
    }
}
