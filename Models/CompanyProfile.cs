using System;
using System.Collections.Generic;

namespace IvaFacilitador.Models
{
    public class CompanyProfile
    {
        public string RealmId { get; set; } = "";
        public DateTime? TariffsReviewedAt { get; set; }
        public List<string> SalesTariffs { get; set; } = new();
    }
}
