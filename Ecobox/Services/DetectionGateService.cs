using Ecobox.Configuration;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class DetectionGateService(IOptions<VisionSettings> options, ILogger<DetectionGateService> logger) : IDetectionGateService
{
    private readonly object _sync = new();
    private CategoriaResiduo? _candidate;
    private int _stableFrames;
    private ulong? _candidateHash;
    private bool _latched;
    private bool _registrationInProgress;
    private DateTime _cooldownUntilUtc = DateTime.MinValue;

    public DetectionGateDecision Evaluate(CategoriaResiduo categoria, decimal confianza, string? roiHash)
    {
        lock (_sync)
        {
            if (_latched)
            {
                logger.LogInformation("ESPERANDO_RETIRO: la detección anterior sigue enclavada.");
                return new(false, "Residuo ya registrado. Retíralo para detectar el siguiente.", "esperando_retiro");
            }

            if (_registrationInProgress)
            {
                return new(false, "Registrando el residuo confirmado...", "registrando");
            }

            if ((double)confianza < options.Value.ConfidenceThreshold)
            {
                ResetCandidateLocked();
                logger.LogWarning("CONFIRMACION_RECHAZADA: confianza {Confidence:P1} menor al umbral {Threshold:P0}.", confianza, options.Value.ConfidenceThreshold);
                return new(false, "Confianza insuficiente.", "baja_confianza", "confianza_insuficiente");
            }

            var hash = ulong.TryParse(roiHash, System.Globalization.NumberStyles.HexNumber, null, out var parsedHash)
                ? parsedHash : (ulong?)null;

            if (_candidate is null)
            {
                _candidate = categoria;
                _candidateHash = hash;
                _stableFrames = 1;
                logger.LogInformation("PRIMERA_PREDICCION: categoría={Category}, confianza={Confidence:P1}, hash={Hash}.", categoria, confianza, roiHash);
                return new(false, "Primer frame clasificado. Esperando el segundo frame estable.", "primer_frame");
            }

            var hashDistance = _candidateHash.HasValue && hash.HasValue
                ? System.Numerics.BitOperations.PopCount(_candidateHash.Value ^ hash.Value)
                : (int?)null;
            logger.LogInformation("SEGUNDA_PREDICCION: categoría={Category}, confianza={Confidence:P1}, distancia_hash={HashDistance}, máximo={MaxDistance}.",
                categoria, confianza, hashDistance, Math.Clamp(options.Value.StableFrameHashDistance, 0, 64));
            string? rejectionReason = _candidate != categoria
                ? "categoria_distinta"
                : hashDistance is null
                    ? "hash_ausente_o_invalido"
                    : hashDistance > Math.Clamp(options.Value.StableFrameHashDistance, 0, 64)
                        ? "hash_demasiado_distinto"
                        : null;

            if (rejectionReason is not null)
            {
                logger.LogWarning("CONFIRMACION_RECHAZADA: motivo={Reason}, categoría anterior={PreviousCategory}, categoría actual={CurrentCategory}, distancia_hash={HashDistance}, máximo={MaxDistance}; el frame actual inicia un nuevo ciclo.",
                    rejectionReason, _candidate, categoria, hashDistance, Math.Clamp(options.Value.StableFrameHashDistance, 0, 64));
                _candidate = categoria;
                _candidateHash = hash;
                _stableFrames = 1;
                return new(false, rejectionReason == "categoria_distinta"
                    ? "Confirmación rechazada: la categoría cambió. Este frame queda como primera predicción."
                    : $"Confirmación rechazada: {rejectionReason}. Este frame queda como primera predicción.", "confirmacion_rechazada", rejectionReason);
            }

            _stableFrames++;
            logger.LogDebug("Predicción compatible; frames estables={StableFrames}.", _stableFrames);

            var required = Math.Max(1, options.Value.StableFramesRequired);
            if (_stableFrames < required)
            {
                return new(false, $"Confirmando clasificación estable ({_stableFrames}/{required}).", "esperando_confirmacion");
            }

            var remaining = _cooldownUntilUtc - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return new(false, $"Esperando {Math.Ceiling(remaining.TotalMilliseconds)} ms antes del siguiente residuo.", "cooldown");
            }

            _registrationInProgress = true;
            logger.LogInformation("CONFIRMACION_EXITOSA: categoría={Category}, confianza={Confidence:P1}, hash_distancia={HashDistance}.", categoria, confianza, hashDistance);
            return new(true, "Clasificación estable confirmada.", "confirmada");
        }
    }

    public void MarkRegistered()
    {
        lock (_sync)
        {
            _registrationInProgress = false;
            _latched = true;
            _cooldownUntilUtc = DateTime.UtcNow.AddMilliseconds(Math.Max(0, options.Value.DetectionCooldownMs));
            logger.LogInformation("ESPERANDO_RETIRO: residuo {Category} registrado; la detección queda enclavada hasta que la cámara quede libre.", _candidate);
        }
    }

    public void CancelPendingRegistration()
    {
        lock (_sync)
        {
            _registrationInProgress = false;
            ResetCandidateLocked();
        }
    }

    public void ResetCandidate()
    {
        lock (_sync)
        {
            if (!_registrationInProgress && !_latched)
            {
                ResetCandidateLocked();
            }
        }
    }

    public void Rearm()
    {
        lock (_sync)
        {
            if (_registrationInProgress)
            {
                return;
            }

            _latched = false;
            ResetCandidateLocked();
            logger.LogDebug("La cámara quedó libre; se habilitó la próxima detección.");
        }
    }

    private void ResetCandidateLocked()
    {
        _candidate = null;
        _candidateHash = null;
        _stableFrames = 0;
    }
}
