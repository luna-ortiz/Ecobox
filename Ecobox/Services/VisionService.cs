using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    ILogger<VisionService> logger) : IVisionService, IHostedService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly object _processLock = new();
    private Process? _process;
    private TaskCompletionSource<bool>? _ready;
    private TaskCompletionSource<string>? _pendingResponse;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private string _statusMessage = "Iniciando proceso de visión…";

    public bool IsReady
    {
        get
        {
            lock (_processLock)
            {
                return _process is { HasExited: false } && _ready?.Task is { IsCompletedSuccessfully: true } task && task.Result;
            }
        }
    }

    public string StatusMessage
    {
        get
        {
            lock (_processLock)
            {
                if (_process is null) return _statusMessage;
                return _process.HasExited ? "Proceso Python detenido." : _statusMessage;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            EnsureProcessStarted();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            lock (_processLock) _statusMessage = "No se pudo iniciar Python.";
            logger.LogError(exception, "No se pudo iniciar el proceso persistente de visión. La aplicación web seguirá disponible.");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await StopProcessAsync();

    public async Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagen);
        var requestStarted = Stopwatch.GetTimestamp();
        var copyStarted = Stopwatch.GetTimestamp();

        try
        {
            await using var buffer = new MemoryStream();
            await imagen.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0 || buffer.Length > 5 * 1024 * 1024)
            {
                return Failure("La imagen está vacía o supera el máximo de 5 MB.");
            }

            var copyMs = Stopwatch.GetElapsedTime(copyStarted).TotalMilliseconds;
            var queueStarted = Stopwatch.GetTimestamp();
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                var queueMs = Stopwatch.GetElapsedTime(queueStarted).TotalMilliseconds;
                EnsureProcessStarted();
                var process = GetRunningProcess();
                var ready = GetReadyTask();
                var isReady = await ready.WaitAsync(TimeSpan.FromMinutes(2), cancellationToken);
                if (!isReady)
                {
                    return Failure("El proceso de visión no pudo cargar el modelo.");
                }

                var responseSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_processLock)
                {
                    _pendingResponse = responseSource;
                }

                var request = new PythonVisionRequest(Convert.ToBase64String(buffer.ToArray()));
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cancellationToken);
                await process.StandardInput.FlushAsync(cancellationToken);

                var rawResponse = await responseSource.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellationToken);
                var result = ParseResult(rawResponse);
                logger.LogDebug("Vision stages: image_copy={CopyMs:F1} ms, request_queue={QueueMs:F1} ms, ASP.NET/Python roundtrip={RoundTripMs:F1} ms, total={TotalMs:F1} ms; {Diagnostic}",
                    copyMs, queueMs, Stopwatch.GetElapsedTime(queueStarted).TotalMilliseconds, Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds, result.Diagnostico);
                return result;
            }
            finally
            {
                lock (_processLock)
                {
                    _pendingResponse = null;
                }

                _requestLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await StopProcessAsync();
            throw;
        }
        catch (TimeoutException exception)
        {
            logger.LogError(exception, "El proceso persistente de visión agotó el tiempo de espera y se reiniciará.");
            await StopProcessAsync();
            return Failure("El servicio de visión tardó demasiado en procesar el frame.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
        {
            logger.LogError(exception, "Falló la comunicación con el proceso persistente de visión.");
            await StopProcessAsync();
            return Failure("No se pudo procesar el frame con el servicio de visión.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopProcessAsync();
        _requestLock.Dispose();
    }

    private void EnsureProcessStarted()
    {
        lock (_processLock)
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            DisposeProcessLocked();
            var settings = options.Value;
            var scriptPath = ResolvePath(settings.ScriptPath);
            var modelPath = ResolvePath(settings.ModelPath);
            var classesPath = ResolvePath(settings.ClassesPath);
            if (!File.Exists(scriptPath) || !File.Exists(modelPath) || !File.Exists(classesPath))
            {
                throw new FileNotFoundException("No se encontró el script de visión, el modelo o clases.json.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveExecutable(settings.PythonExecutable),
                WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            startInfo.Environment["PYTHONUTF8"] = "1";
            startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(modelPath);
            startInfo.ArgumentList.Add("--classes");
            startInfo.ArgumentList.Add(classesPath);
            startInfo.ArgumentList.Add("--threshold");
            startInfo.ArgumentList.Add(settings.ConfidenceThreshold.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--roi-size-ratio");
            startInfo.ArgumentList.Add(Math.Clamp(settings.RoiSizeRatio, 0.1, 1.0).ToString(CultureInfo.InvariantCulture));
            if (settings.SaveDebugFrames)
            {
                var debugDirectory = string.IsNullOrWhiteSpace(settings.DebugFrameDirectory)
                    ? Path.Combine(Path.GetTempPath(), "EcoBoxVisionDebugFrames")
                    : ResolvePath(settings.DebugFrameDirectory);
                startInfo.ArgumentList.Add("--save-debug-frames");
                startInfo.ArgumentList.Add("--debug-dir");
                startInfo.ArgumentList.Add(debugDirectory);
                startInfo.ArgumentList.Add("--debug-frame-limit");
                startInfo.ArgumentList.Add(Math.Clamp(settings.DebugFrameLimit, 1, 10).ToString(CultureInfo.InvariantCulture));
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Start();
            _process = process;
            _statusMessage = "Cargando modelo TensorFlow/Keras…";
            _ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _stdoutPump = PumpStandardOutputAsync(process, _ready);
            _stderrPump = PumpStandardErrorAsync(process);
            logger.LogInformation("Proceso persistente de visión iniciado con Python: {PythonExecutable}.", startInfo.FileName);
        }
    }

    private async Task PumpStandardOutputAsync(Process process, TaskCompletionSource<bool> readySource)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                PythonVisionResponse? message;
                try
                {
                    message = JsonSerializer.Deserialize<PythonVisionResponse>(line, JsonOptions);
                }
                catch (JsonException exception)
                {
                    logger.LogError(exception, "El proceso de visión escribió una línea que no es JSON.");
                    continue;
                }

                if (message?.Tipo == "ready")
                {
                    if (!message.Exito)
                    {
                        lock (_processLock) _statusMessage = "No se pudo cargar el modelo.";
                        logger.LogError("El proceso de visión no pudo cargar el modelo: {Message}", message.Mensaje);
                    }
                    else
                    {
                        lock (_processLock) _statusMessage = "IA activa.";
                        logger.LogInformation("Proceso Python listo; modelo y clases cargados una sola vez.");
                    }

                    readySource.TrySetResult(message.Exito);
                    continue;
                }

                TaskCompletionSource<string>? responseSource;
                lock (_processLock)
                {
                    responseSource = _pendingResponse;
                }

                responseSource?.TrySetResult(line);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            logger.LogDebug(exception, "La salida del proceso de visión terminó.");
        }
        finally
        {
            readySource.TrySetResult(false);
            lock (_processLock)
            {
                if (_process == process && !process.HasExited) _statusMessage = "Proceso Python detenido.";
                _pendingResponse?.TrySetException(new IOException("El proceso Python de visión terminó."));
            }
        }
    }

    private async Task PumpStandardErrorAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("ROI detectada:", StringComparison.Ordinal))
                    logger.LogInformation("{VisionDiagnostic}", line);
                else
                    logger.LogDebug("Python vision stderr: {StandardError}", line);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            logger.LogDebug(exception, "La salida de error del proceso de visión terminó.");
        }
    }

    private VisionResult ParseResult(string rawResponse)
    {
        var response = JsonSerializer.Deserialize<PythonVisionResponse>(rawResponse, JsonOptions);
        if (response is null || !double.IsFinite(response.Confianza) || response.Confianza < 0 || response.Confianza > 1)
        {
            return Failure("El servicio de visión devolvió una confianza inválida.");
        }

        var category = response.Categoria?.Trim().ToUpperInvariant() switch
        {
            "APROVECHABLE" => "APROVECHABLE",
            "NO_APROVECHABLE" => "NO_APROVECHABLE",
            _ => null
        };
        if (!response.Exito || category is null || response.Confianza < options.Value.ConfidenceThreshold)
        {
            return new VisionResult
            {
                Prediccion = response.Prediccion,
                RoiHash = response.RoiHash,
                Diagnostico = response.DiagnosticText(),
                Confianza = (decimal)response.Confianza,
                Exito = false,
                Mensaje = response.Confianza < options.Value.ConfidenceThreshold
                    ? "Confianza insuficiente."
                    : response.Mensaje
            };
        }

        return new VisionResult
        {
            Categoria = category,
            Prediccion = response.Prediccion,
            RoiHash = response.RoiHash,
            Diagnostico = response.DiagnosticText(),
            Confianza = (decimal)response.Confianza,
            Exito = true,
            Mensaje = response.Mensaje
        };
    }

    private Process GetRunningProcess()
    {
        lock (_processLock)
        {
            return _process is { HasExited: false } process
                ? process
                : throw new InvalidOperationException("El proceso de visión no está activo.");
        }
    }

    private Task<bool> GetReadyTask()
    {
        lock (_processLock)
        {
            return _ready?.Task ?? Task.FromResult(false);
        }
    }

    private string ResolvePath(string path) => Path.IsPathRooted(path)
        ? path
        : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));

    private string ResolveExecutable(string executable) => Path.IsPathRooted(executable)
        ? executable
        : executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar)
            ? ResolvePath(executable)
            : executable;

    private async Task StopProcessAsync()
    {
        Process? process;
        lock (_processLock)
        {
            process = _process;
            _process = null;
            _ready?.TrySetResult(false);
            _pendingResponse?.TrySetException(new IOException("El proceso de visión se está cerrando."));
            _pendingResponse = null;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            logger.LogDebug(exception, "Error al detener el proceso Python de visión.");
        }
        finally
        {
            process.Dispose();
        }
    }

    private void DisposeProcessLocked()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogDebug(exception, "No se pudo limpiar un proceso anterior de visión.");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private static VisionResult Failure(string message) => new() { Exito = false, Mensaje = message };

    private sealed record PythonVisionRequest([property: JsonPropertyName("imagen_base64")] string ImagenBase64)
    {
        [JsonPropertyName("id")]
        public string Id { get; } = Guid.NewGuid().ToString("N");
    }

    private sealed class PythonVisionResponse
    {
        [JsonPropertyName("tipo")] public string? Tipo { get; init; }
        [JsonPropertyName("exito")] public bool Exito { get; init; }
        [JsonPropertyName("categoria")] public string? Categoria { get; init; }
        [JsonPropertyName("prediccion")] public string? Prediccion { get; init; }
        [JsonPropertyName("roi_hash")] public string? RoiHash { get; init; }
        [JsonPropertyName("roi")] public RoiInfo? Roi { get; init; }
        [JsonPropertyName("timings_ms")] public TimingInfo? Timings { get; init; }
        [JsonPropertyName("confianza")] public double Confianza { get; init; }
        [JsonPropertyName("mensaje")] public string Mensaje { get; init; } = string.Empty;

        public string DiagnosticText() => $"ROI {Roi?.X},{Roi?.Y} {Roi?.Width}x{Roi?.Height}/{Roi?.SourceWidth}x{Roi?.SourceHeight}; 224x224; roi_crop={Timings?.RoiCrop:F1}ms preprocess={Timings?.Preprocess:F1}ms inference={Timings?.Inference:F1}ms python={Timings?.TotalPython:F1}ms";
    }

    private sealed class RoiInfo
    {
        [JsonPropertyName("x")] public int X { get; init; }
        [JsonPropertyName("y")] public int Y { get; init; }
        [JsonPropertyName("width")] public int Width { get; init; }
        [JsonPropertyName("height")] public int Height { get; init; }
        [JsonPropertyName("source_width")] public int SourceWidth { get; init; }
        [JsonPropertyName("source_height")] public int SourceHeight { get; init; }
    }

    private sealed class TimingInfo
    {
        [JsonPropertyName("roi_crop")] public double RoiCrop { get; init; }
        [JsonPropertyName("preprocess")] public double Preprocess { get; init; }
        [JsonPropertyName("inference")] public double Inference { get; init; }
        [JsonPropertyName("total_python")] public double TotalPython { get; init; }
    }
}
