using System;
using System.Collections.Generic;

namespace IvaFacilitador.Models
{
    public class CompanyProfile
    {
        public string RealmId { get; set; } = "";

        // Tariffs detected from QuickBooks
        public List<string> SalesTariffs { get; set; } = new();
        public DateTimeOffset? TariffsReviewedAt { get; set; }

        // Datáfonos (card terminals) accounts
        public List<QboAccount> DatafonoAccounts { get; set; } = new();

        // Whether the company operates under MEIC or MAG certificates
        public bool UsesMeic { get; set; }
        public bool UsesMag { get; set; }

        // Prorrata settings
        public bool UsesProrrata { get; set; }

        // IVA related accounts
        public QboAccount? IvaVentasAccount { get; set; }
        public QboAccount? IvaComprasAccount { get; set; }
        public QboAccount? IvaGastoAccount { get; set; }

        // Additional flags
        public bool HandlesExports { get; set; }
        public bool HandlesCapitalRentals { get; set; }
        public bool HasNonDeductibleExpenses { get; set; }
        public bool HasExonerations { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }
    }
}
