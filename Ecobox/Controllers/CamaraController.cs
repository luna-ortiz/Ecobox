using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ecobox.Configuration;
using System.Diagnostics;

namespace Ecobox.Controllers;

public sealed class CamaraController(
    IPresenceDetectionService presenceDetectionService,
    IClasificacionResiduoService clasificacionService,
    IVisionService visionService,
    IDetectionGateService detectionGate,
    IOptions<VisionSettings> visionSettings,
    ILogger<CamaraController> logger) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index()
    {
        var presence = presenceDetectionService.GetCurrentState();
        var settings = visionSettings.Value;
        return View(new CamaraViewModel
        {
            EstadoDeteccion = presence.Mensaje,
            FrameIntervalMs = Math.Clamp(settings.FrameIntervalMs, 100, 5000),
            StableFramesRequired = Math.Clamp(settings.StableFramesRequired, 1, 10),
            DetectionCooldownMs = Math.Max(0, settings.DetectionCooldownMs),
            PresenceChangeThreshold = Math.Clamp(settings.PresenceChangeThreshold, 0.01, 1.0),
            RoiSizeRatio = Math.Clamp(settings.RoiSizeRatio, 0.1, 1.0)
        });
    }

    [HttpGet]
    public IActionResult EstadoVision() => Ok(new { iaActiva = visionService.IsReady, mensaje = visionService.StatusMessage });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RearmarDeteccion()
    {
        detectionGate.Rearm();
        return Ok(new { rearmada = true });
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

        var started = Stopwatch.GetTimestamp();
        logger.LogDebug("Frame recibido: {Bytes} bytes, cliente captura={CaptureMs} ms, JPEG={JpegMs} ms.",
            imagen.Length, Request.Headers["X-Client-Capture-Ms"], Request.Headers["X-Client-Jpeg-Ms"]);
        await using var stream = imagen.OpenReadStream();
        var resultado = await clasificacionService.ProcesarImagenAsync(stream, cancellationToken);
        logger.LogDebug("Solicitud /Camara/Clasificar completa en {ElapsedMs:F1} ms; registrada={Registered}; categoría={Category}.",
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, resultado.RegistroGuardado, resultado.Categoria);
        return Ok(new
        {
            exito = resultado.Exito,
            registrada = resultado.RegistroGuardado,
            estado = resultado.Estado,
            razon = resultado.Razon,
            categoria = resultado.Categoria?.ToString(),
            confianza = resultado.Confianza,
            mensaje = resultado.Mensaje
        });
    }
}
