namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public enum PayPeriod { Mensual = 1, Quincenal = 2, Semanal = 3 }

    public class Collaborator
    {
        public int    Id               { get; set; }
        public int    CompanyId        { get; set; }
        public string Name             { get; set; } = "";
        public string? TaxId           { get; set; }  // cédula
        public string? Sector          { get; set; }  // texto libre o nombre sector
        public string? Position        { get; set; }  // cargo
        public decimal? MonthlySalary  { get; set; }
        public PayPeriod PayPeriod     { get; set; } = PayPeriod.Mensual;
        public int? Split1             { get; set; }  // %
        public int? Split2             { get; set; }
        public int? Split3             { get; set; }
        public int? Split4             { get; set; }
        public bool UseCCSS            { get; set; }
        public bool UseSeguro          { get; set; }
        public int  Status             { get; set; }  // 0=Activo, 1=Inactivo
        public string? QboEmployeeId   { get; set; }  // vínculo en QBO
    }
}
