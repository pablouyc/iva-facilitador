using System;
using System.Threading;
using System.Threading.Tasks;

namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollAuthService
    {
        Task<(string realmId, string accessToken)> GetRealmAndValidAccessTokenAsync(int companyId, CancellationToken ct = default);

        Task<(string? realmId, string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)>
            ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default);

        Task SaveTokensAsync(
            int companyId,
            string? realmId,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAtUtc,
            CancellationToken ct = default);

        Task<string?> TryGetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
    }
}
