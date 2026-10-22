namespace Ecobox.Services.Interfaces;

public interface IDatabaseStatusService
{
    bool IsConfigured { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
