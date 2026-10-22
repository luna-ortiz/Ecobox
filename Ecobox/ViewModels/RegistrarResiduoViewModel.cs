using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ecobox.ViewModels;

public sealed class RegistrarResiduoViewModel
{
    [Required(ErrorMessage = "Selecciona el tipo de residuo.")]
    [Display(Name = "Tipo de residuo")]
    public string Clasificacion { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un contenedor.")]
    [Display(Name = "Contenedor")]
    public int IdContenedor { get; set; }

    [Range(typeof(decimal), "0", "1", ErrorMessage = "La confianza debe estar entre 0 y 1.")]
    [Display(Name = "Confianza de clasificación")]
    public decimal? ConfianzaClasificacion { get; set; } = 0.95m;

    [ValidateNever]
    public IReadOnlyList<SelectListItem> TiposResiduo { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<SelectListItem> Contenedores { get; set; } = [];
}
