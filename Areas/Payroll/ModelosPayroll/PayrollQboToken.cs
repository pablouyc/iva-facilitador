using System;

namespace IvaFacilitador.Areas.Payroll.ModelosPayroll;

public class PayrollQboToken
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string RealmId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
