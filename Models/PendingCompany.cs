namespace IvaFacilitador.Models;

public class PendingCompany
{
    public string RealmId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
