using Ecobox.Data;
using Ecobox.Services.Interfaces;

namespace Ecobox.Services;

public sealed class DatabaseStatusService(EcoBoxDbContext dbContext, IConfiguration configuration, ILogger<DatabaseStatusService> logger)
    : IDatabaseStatusService
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration.GetConnectionString("EcoBox"));

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No se pudo comprobar la conexión con PostgreSQL.");
            return false;
        }
    }
}
