namespace IvaFacilitador.Domain.Payroll
{
    public class PayrollOptions
    {
        public string Culture  { get; set; } = "es-CR";
        public string Timezone { get; set; } = "America/Costa_Rica";
        public string Currency { get; set; } = "CRC";
        public int    Rounding { get; set; } = 2;
    }
}
