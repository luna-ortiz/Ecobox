using Ecobox.Models;

namespace Ecobox.Services.Interfaces;

public interface IDetectionGateService
{
    DetectionGateDecision Evaluate(CategoriaResiduo categoria, decimal confianza, string? roiHash);

    void MarkRegistered();

    void CancelPendingRegistration();

    void ResetCandidate();

    void Rearm();
}

public sealed record DetectionGateDecision(bool Confirmed, string Message, string State, string? Reason = null);
