using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Ecobox.Controllers;

public sealed class CamaraController(
    IPresenceDetectionService presenceDetectionService,
    IClasificacionResiduoService clasificacionService) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index()
    {
        var presence = presenceDetectionService.GetCurrentState();
        return View(new CamaraViewModel { EstadoDeteccion = presence.Mensaje });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImageBytes)]
    public async Task<IActionResult> Clasificar(IFormFile imagen, CancellationToken cancellationToken)
    {
        if (imagen is null || imagen.Length == 0 || imagen.Length > MaxImageBytes)
        {
            return BadRequest(new { exito = false, mensaje = "La imagen está vacía o supera el máximo de 5 MB." });
        }

        if (!string.Equals(imagen.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { exito = false, mensaje = "La captura debe estar en formato JPEG." });
        }

        await using var stream = imagen.OpenReadStream();
        var resultado = await clasificacionService.ProcesarImagenAsync(stream, cancellationToken);
        return Ok(new
        {
            exito = resultado.Exito,
            registrada = resultado.RegistroGuardado,
            categoria = resultado.Categoria?.ToString(),
            confianza = resultado.Confianza,
            mensaje = resultado.Mensaje
        });
    }
}
