using System;
using System.Threading;
using System.Threading.Tasks;

namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollAuthService
    {
        // Devuelve (realmId, accessToken) válido para la empresa (refresca si hace falta)
        Task<(string realmId, string accessToken)> GetRealmAndValidAccessTokenAsync(
            int companyId,
            CancellationToken ct = default
        );

        // Intercambia el 'code' del callback por tokens de acceso/refresh
        Task<(string? realmId, string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)>
            ExchangeCodeAsync(
                string code,
                string redirectUri,
                CancellationToken ct = default
            );

        // Persiste tokens para la empresa especificada (crea un nuevo registro histórico)
        Task SaveTokensAsync(
            int companyId,
            string? realmId,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAtUtc,
            CancellationToken ct = default
        );

        // Intenta obtener el nombre de la compañía desde CompanyInfo (opcional)
        Task<string?> TryGetCompanyNameAsync(
            string realmId,
            string accessToken,
            CancellationToken ct = default
        );
    }
}
