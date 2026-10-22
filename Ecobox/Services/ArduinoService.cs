using Ecobox.Configuration;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;

namespace Ecobox.Services;

public sealed class ArduinoService : IArduinoService
{
    public Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default)
    {
        if (!ClasificacionesResiduo.Permitidas.Contains(clasificacion, StringComparer.Ordinal))
        {
            return Task.FromResult(new ArduinoResult
            {
                Exito = false,
                Comando = "ERROR",
                Mensaje = "La clasificación recibida no está permitida."
            });
        }

        return Task.FromResult(new ArduinoResult
        {
            Exito = false,
            Comando = clasificacion,
            Mensaje = "La comunicación está preparada conceptualmente; falta confirmar el modelo de placa, puerto COM y mecanismo."
        });
    }
}
