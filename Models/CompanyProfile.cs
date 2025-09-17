using System;
using System.Collections.Generic;

namespace IvaFacilitador.Models
{
    public class CompanyProfile
    {
        public string RealmId { get; set; } = string.Empty;

        // 1) Tarifas ventas
        public List<string> SalesTariffs { get; set; } = new();
        public DateTime? TariffsReviewedAt { get; set; }

        // 2) Datáfonos (POS/adquirencia)
        public bool UsesPos { get; set; }
        public string? PosWithholdingIncomeAccountId { get; set; }
        public string? PosWithholdingVatAccountId { get; set; }
        public string? PosFeesAccountId { get; set; }

        // 3) Registro MEIC/MAG
        public bool IsMeicMag { get; set; }

        // 4) Prorrata
        public bool UsesProrrata { get; set; }
        public decimal? ProrrataPercent { get; set; } // 0-100
        public bool ComputeProrrataAutomatically { get; set; }
        public string? ProrrataFrequency { get; set; } // mensual/trimestral/anual

        // 5) Cuentas IVA
        public string? IvaControlAccountId { get; set; }
        public string? IvaPayableAccountId { get; set; }
        public string? IvaReceivableAccountId { get; set; }

        // 6) Exportaciones
        public bool HasExports { get; set; }

        // 7) Alquileres de bienes de capital
        public bool HasCapitalRentals { get; set; }

        // 8) Gastos no deducibles
        public List<string> NonDeductibleExpenseAccountIds { get; set; } = new();

        // 9) Exoneraciones
        public List<Exemption> Exemptions { get; set; } = new();

        public class Exemption
        {
            public string Type { get; set; } = "";    // Zona Franca / EXONET / Gobierno / Otra
            public decimal Percent { get; set; }      // 0-100
            public DateTime? ValidUntil { get; set; }
            public string? CertificateNumber { get; set; }
        }
    }
}