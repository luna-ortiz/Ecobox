using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ecobox.Configuration;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class VisionService(
    IOptions<VisionSettings> options,
    IWebHostEnvironment environment,
    ILogger<VisionService> logger) : IVisionService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagen);

        var settings = options.Value;
        var scriptPath = ResolvePath(settings.ScriptPath);
        var modelPath = ResolvePath(settings.ModelPath);
        if (!File.Exists(scriptPath) || !File.Exists(modelPath))
        {
            return Failure("No se encontró el script de inferencia o el modelo configurado.");
        }

        var temporaryImage = Path.Combine(Path.GetTempPath(), $"ecobox-{Guid.NewGuid():N}.jpg");
        try
        {
            await using (var output = new FileStream(temporaryImage, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await imagen.CopyToAsync(output, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = settings.PythonExecutable,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(temporaryImage);
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(modelPath);
            startInfo.ArgumentList.Add("--threshold");
            startInfo.ArgumentList.Add(settings.ConfidenceThreshold.ToString(CultureInfo.InvariantCulture));

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                throw;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogDebug("Python vision stderr: {StandardError}", stderr.Trim());
            }

            if (process.ExitCode != 0)
            {
                logger.LogWarning("Python vision process exited with code {ExitCode}: {StandardError}", process.ExitCode, stderr.Trim());
                return Failure("El proceso de visión no pudo completar la clasificación.");
            }

            PythonVisionResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<PythonVisionResponse>(stdout, JsonOptions);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Python vision returned invalid JSON.");
                return Failure("El servicio de visión devolvió una respuesta inválida.");
            }

            if (response is null || !double.IsFinite(response.Confianza) || response.Confianza < 0 || response.Confianza > 1)
            {
                return Failure("El servicio de visión devolvió una confianza inválida.");
            }

            var category = NormalizeCategory(response.Categoria);
            if (!response.Exito || category is null || response.Confianza < settings.ConfidenceThreshold)
            {
                return new VisionResult
                {
                    Confianza = (decimal)response.Confianza,
                    Exito = false,
                    Mensaje = response.Confianza < settings.ConfidenceThreshold
                        ? "Confianza insuficiente. No se registró el residuo ni se envió un comando a Arduino."
                        : response.Mensaje
                };
            }

            return new VisionResult
            {
                Categoria = category,
                Confianza = (decimal)response.Confianza,
                Exito = true,
                Mensaje = response.Mensaje
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(exception, "Could not run Python vision inference.");
            return Failure("No se pudo ejecutar el servicio de visión. Revisa la configuración de Python.");
        }
        finally
        {
            try { File.Delete(temporaryImage); }
            catch (IOException exception) { logger.LogWarning(exception, "Could not remove temporary vision image."); }
            catch (UnauthorizedAccessException exception) { logger.LogWarning(exception, "Could not remove temporary vision image."); }
        }
    }

    private string ResolvePath(string path) => Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));

    private static string? NormalizeCategory(string? category) => category?.Trim().ToUpperInvariant() switch
    {
        "APROVECHABLE" => "APROVECHABLE",
        "NO_APROVECHABLE" => "NO_APROVECHABLE",
        _ => null
    };

    private static VisionResult Failure(string message) => new() { Exito = false, Mensaje = message };

    private sealed class PythonVisionResponse
    {
        [JsonPropertyName("exito")] public bool Exito { get; init; }
        [JsonPropertyName("categoria")] public string? Categoria { get; init; }
        [JsonPropertyName("confianza")] public double Confianza { get; init; }
        [JsonPropertyName("mensaje")] public string Mensaje { get; init; } = string.Empty;
    }
}
