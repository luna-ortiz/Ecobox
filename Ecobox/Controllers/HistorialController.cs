using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Ecobox.Controllers;

public sealed class HistorialController(IRegistroResiduoService registroService) : Controller
{
    [HttpGet("/Historial")]
    [HttpGet("/Residuo")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var registros = await registroService.ObtenerHistorialAsync(cancellationToken);
        return View(registros.Select(RegistroResiduoViewModel.Desde).ToList());
    }
}
