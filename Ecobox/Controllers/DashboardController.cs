using Ecobox.Services.Interfaces;
using Ecobox.Models;
using Ecobox.Services.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ecobox.Controllers;

public class DashboardController(IDashboardService dashboardService, IRegistroResiduoService registroService, ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await dashboardService.GetDashboardAsync(cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VaciarCaneca([BindRequired] CategoriaResiduo? caneca, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || caneca is null || !Enum.IsDefined(caneca.Value)) return BadRequest();
        var categoria = caneca.Value;
        try
        {
            await registroService.VaciarAsync(categoria, cancellationToken);
            TempData["MensajeCapacidad"] = $"Caneca {categoria} vaciada. El historial se conservó.";
            TempData["TipoMensajeCapacidad"] = "success";
            return RedirectToAction(nameof(Index));
        }
        catch (CanecaNoEncontradaException exception)
        {
            logger.LogError(exception, "No existe estado de capacidad para {Caneca}.", categoria);
            return NotFound();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "No se pudo vaciar la caneca {Caneca}.", categoria);
            TempData["MensajeCapacidad"] = "No se pudo actualizar la ocupación de la caneca.";
            TempData["TipoMensajeCapacidad"] = "danger";
            return RedirectToAction(nameof(Index));
        }
    }
}
