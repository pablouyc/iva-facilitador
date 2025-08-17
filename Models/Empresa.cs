using System.ComponentModel.DataAnnotations;

namespace IvaFacilitador.Models;

public class Empresa
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string RealmId { get; set; } = string.Empty;
    public string Moneda { get; set; } = "CRC";
    public string Pais { get; set; } = "CR";
    public string PeriodoIva { get; set; } = "Mensual";
    public decimal PorcentajeIvaDefault { get; set; } = 0.13m;
    public string MetodoRedondeo { get; set; } = "Matematico";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ConexionQbo ConexionQbo { get; set; } = new();
}
