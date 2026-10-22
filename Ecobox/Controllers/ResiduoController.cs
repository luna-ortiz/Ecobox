using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ecobox.Controllers;

public class ResiduoController(
    IResiduoService residuoService,
    IContenedorService contenedorService,
    IDatabaseStatusService databaseStatus,
    ILogger<ResiduoController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var available = await databaseStatus.IsAvailableAsync(cancellationToken);
        ViewBag.MensajeBaseDatos = available
            ? "Historial consultado desde PostgreSQL."
            : databaseStatus.IsConfigured
                ? "PostgreSQL no está disponible. No se cargaron registros."
                : "PostgreSQL aún no está configurado. No hay registros para mostrar.";
        var residuos = available
            ? await residuoService.ObtenerHistorialAsync(cancellationToken)
            : [];

        return View(residuos.Select(ResiduoViewModel.Desde).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Registrar(CancellationToken cancellationToken)
    {
        if (!await databaseStatus.IsAvailableAsync(cancellationToken))
        {
            TempData["MensajeError"] = "No se puede registrar el residuo porque PostgreSQL no está disponible.";
            return RedirectToAction(nameof(Index));
        }

        var viewModel = await CargarOpcionesAsync(new RegistrarResiduoViewModel(), cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistrarResiduoViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!await databaseStatus.IsAvailableAsync(cancellationToken))
        {
            TempData["MensajeError"] = "No se puede registrar el residuo porque PostgreSQL no está disponible.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await CargarOpcionesAsync(viewModel, cancellationToken);
            return View(viewModel);
        }

        try
        {
            await residuoService.RegistrarAsync(
                viewModel.Clasificacion,
                viewModel.IdContenedor,
                viewModel.ConfianzaClasificacion ?? 0.95m,
                cancellationToken: cancellationToken);

            TempData["MensajeExito"] = "El residuo se registró correctamente. El historial y el dashboard ya muestran los datos actualizados.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(viewModel.Clasificacion), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No fue posible registrar manualmente el residuo.");
            ModelState.AddModelError(string.Empty, "No se pudo guardar el residuo. Verifica la conexión con PostgreSQL e inténtalo de nuevo.");
        }

        await CargarOpcionesAsync(viewModel, cancellationToken);
        return View(viewModel);
    }

    private async Task<RegistrarResiduoViewModel> CargarOpcionesAsync(
        RegistrarResiduoViewModel viewModel,
        CancellationToken cancellationToken)
    {
        viewModel.TiposResiduo =
        [
            new SelectListItem("APROVECHABLE", "APROVECHABLE"),
            new SelectListItem("NO_APROVECHABLE", "NO_APROVECHABLE")
        ];

        var contenedores = await contenedorService.ObtenerTodosAsync(cancellationToken);
        viewModel.Contenedores = contenedores
            .Select(contenedor => new SelectListItem(contenedor.Nombre, contenedor.IdContenedor.ToString()))
            .ToList();

        return viewModel;
    }
}
