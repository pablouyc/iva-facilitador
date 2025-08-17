using IvaFacilitador.Models;
using Microsoft.AspNetCore.Http;

namespace IvaFacilitador.Services;

public interface IQboAuthService
{
    string GetAuthorizationUrl(string returnUrl);
    Task<PendingCompany> HandleCallbackAsync(HttpRequest request);
}

public class QboAuthService : IQboAuthService
{
    public string GetAuthorizationUrl(string returnUrl)
    {
        // In real implementation, redirect to QBO OAuth page
        var callback = $"/Auth/QboCallback?realmId=123&companyName=DemoCo&access_token=at&refresh_token=rt&expires_in=3600&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return callback;
    }

    public Task<PendingCompany> HandleCallbackAsync(HttpRequest request)
    {
        var realmId = request.Query["realmId"].ToString();
        var companyName = request.Query["companyName"].ToString();
        var accessToken = request.Query["access_token"].ToString();
        var refreshToken = request.Query["refresh_token"].ToString();
        var expiresIn = int.TryParse(request.Query["expires_in"], out var exp) ? exp : 3600;
        var pc = new PendingCompany
        {
            RealmId = realmId,
            CompanyName = companyName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn)
        };
        return Task.FromResult(pc);
    }
}
