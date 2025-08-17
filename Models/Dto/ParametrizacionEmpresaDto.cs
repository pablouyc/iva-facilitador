using System.ComponentModel.DataAnnotations;

namespace IvaFacilitador.Models.Dto;

public class ParametrizacionEmpresaDto
{
    [Required] public string Moneda { get; set; } = "CRC";
    [Required] public string Pais { get; set; } = "CR";
    [Required] public string PeriodoIva { get; set; } = "Mensual"; // Mensual/Trimestral
    [Range(0,1)] public decimal PorcentajeIvaDefault { get; set; } = 0.13m;
    [Required] public string MetodoRedondeo { get; set; } = "Matematico"; // Matematico/Superior/Inferior
}
