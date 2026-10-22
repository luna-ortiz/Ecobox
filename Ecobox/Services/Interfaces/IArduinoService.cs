using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IArduinoService
{
    Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default);
}
