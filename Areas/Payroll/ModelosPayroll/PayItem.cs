namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class PayItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "earning"; // earning|deduction|other
        public bool IsRecurring { get; set; }
    }
}
