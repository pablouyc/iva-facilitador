namespace IvaFacilitador.Areas.Payroll.ModelosPayroll
{
    public class PayrollQboToken
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string? RealmId { get; set; }
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string? TokenType { get; set; }
        public string? Scope { get; set; }
        public System.DateTime ExpiresAtUtc { get; set; }
    }
}
