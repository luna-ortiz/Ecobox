using Ecobox.Services.Results;

namespace Ecobox.ViewModels;

public sealed class HistorialViewModel
{
    public IReadOnlyList<RegistroResiduoViewModel> Registros { get; init; } = [];
    public IReadOnlyList<CicloCanecaResult> Ciclos { get; init; } = [];
}
