using System.Collections.Generic;

namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? TaxId { get; set; }
        public string? QboId { get; set; }
        public string? PayPolicy { get; set; }
        public string? ObligationsNotes { get; set; }

        public List<Employee> Employees { get; set; } = new();
        public List<PayPeriod> PayPeriods { get; set; } = new();
        public List<PayEvent> PayEvents { get; set; } = new();
    }
}

