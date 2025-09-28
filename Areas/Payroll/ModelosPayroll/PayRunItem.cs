namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class PayRunItem
    {
        public int Id { get; set; }

        public int PeriodId { get; set; }
        public PayPeriod Period { get; set; } = null!;

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public int ItemId { get; set; }
        public PayItem Item { get; set; } = null!;

        public decimal Amount { get; set; }
    }
}
