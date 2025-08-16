using System;
using System.Collections.Generic;

namespace IvaFacilitador.Models
{
    /// <summary>
    /// Stores company configuration captured during onboarding.
    /// </summary>
    public class CompanyProfile
    {
        public string RealmId { get; set; } = "";

        // PASO 1 — Tarifas
        public List<string> SalesTariffs { get; set; } = new();
        public DateTimeOffset? TariffsReviewedAt { get; set; }

        /// <summary>
        /// CSV representation for backward compatibility.
        /// </summary>
        public string TariffsCsv
        {
            get => string.Join(",", SalesTariffs);
            set
            {
                SalesTariffs = string.IsNullOrWhiteSpace(value)
                    ? new List<string>()
                    : new List<string>(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        // PASO 2 — General
        public bool HasCardTerminals { get; set; }

        // Cuentas de datáfonos (visibles solo si HasCardTerminals == true)
        public string? CardRetencionRentaAccount { get; set; }
        public string? CardRetencionIvaAccount { get; set; }
        public string? CardComisionesAccount { get; set; }

        public bool IsMEICRegistered { get; set; }
        public bool IsMAGRegistered { get; set; }

        // Prorrata (solo si aplica)
        public bool? HasProrata { get; set; }
        public decimal? ProrataPercent { get; set; }
        public string? ProrataCalcMode { get; set; }

        // *** TRES CUENTAS DE IVA (todas requeridas) ***
        public string IvaControlAccount { get; set; } = "";
        public string IvaPorPagarAccount { get; set; } = "";
        public string IvaAFavorAccount { get; set; } = "";

        public bool DoesExports { get; set; }
        public bool HasCapitalRentals { get; set; }

        public string? NonDeductibleExpensesAccount { get; set; }

        public bool InExemptionZone { get; set; }
        public string? ExemptionNotes { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }
    }
}
