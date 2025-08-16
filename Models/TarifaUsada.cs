using System;

namespace IvaFacilitador.Models
{
    public class TarifaUsada
    {
        public string? TaxCodeId { get; set; }
        public string? TaxCodeName { get; set; }
        public string? TaxRateId { get; set; }
        public string? TaxRateName { get; set; }
        public decimal Porcentaje { get; set; }
        public int CountTransacciones { get; set; }
        public decimal SumBase { get; set; }
        public decimal SumImpuesto { get; set; }
        public DateTime? PrimerUso { get; set; }
        public DateTime? UltimoUso { get; set; }
    }
}
