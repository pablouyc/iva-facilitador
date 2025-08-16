using System;

namespace IvaFacilitador.Models
{
    public class TarifaUsada
    {
        public string? TaxCode { get; set; }
        public string? TaxRate { get; set; }
        public int CountTransacciones { get; set; }
        public decimal SumBase { get; set; }
        public decimal SumImpuesto { get; set; }
        public DateTime? PrimerUso { get; set; }
        public DateTime? UltimoUso { get; set; }
    }
}
