using System;

namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class PayEvent
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int ItemId { get; set; }
        public PayItem Item { get; set; } = null!;

        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }
}

