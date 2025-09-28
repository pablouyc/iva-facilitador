using System;

namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class Employee
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public string NationalId { get; set; } = string.Empty;
        public string FirstName  { get; set; } = string.Empty;
        public string LastName   { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }

        public string? Email  { get; set; }
        public string Status  { get; set; } = "Active";
        public decimal? BaseSalary { get; set; }
    }
}
