using System;
using System.Collections.Generic;

namespace IvaFacilitador.Models
{
    public class CompanyProfile
    {
        public string RealmId { get; set; } = "";
        public List<string> SalesTariffs { get; set; } = new();
        public DateTimeOffset? TariffsReviewedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
    }
}
