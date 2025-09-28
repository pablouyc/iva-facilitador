using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IvaFacilitador.Payroll.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TaxId { get; set; }

        // Realm/Company Id de QBO (Payroll)
        [MaxLength(100)]
        public string? QboId { get; set; }

        public string? PayPolicy { get; set; }
        public string? ObligationsNotes { get; set; }
    }

    public class Employee
    {
        public int Id { get; set; }
        [Required] public int CompanyId { get; set; }
        [Required, MaxLength(50)] public string NationalId { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
        [Required] public DateTime JoinDate { get; set; }
        [EmailAddress] public string? Email { get; set; }
        [Required, MaxLength(20)] public string Status { get; set; } = "Active";
        public string? BaseSalary { get; set; }

        public Company? Company { get; set; }
    }

    public class PayItem
    {
        public int Id { get; set; }
        [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string Type { get; set; } = "Earning";
        [Required] public bool IsRecurring { get; set; } = false;
    }

    public class PayPeriod
    {
        public int Id { get; set; }
        [Required] public int CompanyId { get; set; }
        [Required] public DateTime StartDate { get; set; }
        [Required] public DateTime EndDate { get; set; }
        [Required, MaxLength(20)] public string Status { get; set; } = "Open";

        public Company? Company { get; set; }
    }

    public class PayEvent
    {
        public int Id { get; set; }
        [Required] public int CompanyId { get; set; }
        public int? EmployeeId { get; set; }
        [Required] public int ItemId { get; set; }
        [Required] public DateTime Date { get; set; }
        [Required] public string Amount { get; set; } = "0";
        public string? Note { get; set; }

        public Company? Company { get; set; }
        public Employee? Employee { get; set; }
        public PayItem? Item { get; set; }
    }

    public class PayRunItem
    {
        public int Id { get; set; }
        [Required] public int PeriodId { get; set; }
        [Required] public int EmployeeId { get; set; }
        [Required] public int ItemId { get; set; }
        [Required] public string Amount { get; set; } = "0";

        public PayPeriod? Period { get; set; }
        public Employee? Employee { get; set; }
        public PayItem? Item { get; set; }
    }

    public class PayrollQboToken
    {
        public int Id { get; set; }
        [Required] public int CompanyId { get; set; }
        [MaxLength(100)] public string? RealmId { get; set; }
        [Required] public string AccessToken { get; set; } = string.Empty;
        [Required] public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }

        public Company? Company { get; set; }
    }
}
