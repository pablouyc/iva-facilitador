using System;

namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class PayPeriod
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate   { get; set; }

        public string Status { get; set; } = "Open"; // Open|Closed|Processed
    }
}
