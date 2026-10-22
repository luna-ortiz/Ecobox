using Ecobox.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecobox.Controllers;

public class DashboardController(IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await dashboardService.GetDashboardAsync(cancellationToken);
        return View(viewModel);
    }
}
