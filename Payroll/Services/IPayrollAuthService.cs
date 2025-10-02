namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollAuthService
    {
        string GetAuthorizeUrl(int companyId, string returnTo);
        System.Threading.Tasks.Task<(string accessToken, string refreshToken, System.DateTime expiresAtUtc, string? realmId)>
            ExchangeCodeAsync(string code, string redirectUri);
        System.Threading.Tasks.Task SaveTokensAsync(int companyId, string? realmId, string accessToken, string refreshToken, System.DateTime expiresAtUtc);
    }
}
