using System;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace IvaFacilitador.Pages.Auth
{
    [IgnoreAntiforgeryToken]
    public class ConnectQboPayrollModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public ConnectQboPayrollModel(IConfiguration cfg) { _cfg = cfg; }

        public IActionResult OnGet(int companyId, string? returnTo = null)
        {
            string Env(string k) => Environment.GetEnvironmentVariable(k) ?? string.Empty;

            string env        = Env("IntuitPayrollAuth__Environment");
            if (string.IsNullOrWhiteSpace(env)) env = _cfg["IntuitPayrollAuth:Environment"] ?? "";
            if (string.IsNullOrWhiteSpace(env)) env = Env("IntuitAuth__Environment");
            if (string.IsNullOrWhiteSpace(env)) env = _cfg["IntuitAuth:Environment"] ?? "production";

            string clientId   = Env("IntuitPayrollAuth__ClientId");
            if (string.IsNullOrWhiteSpace(clientId)) clientId = _cfg["IntuitPayrollAuth:ClientId"] ?? "";
            if (string.IsNullOrWhiteSpace(clientId)) clientId = Env("IntuitAuth__ClientId");
            if (string.IsNullOrWhiteSpace(clientId)) clientId = _cfg["IntuitAuth:ClientId"] ?? "";

            string redirectUri= Env("IntuitPayrollAuth__RedirectUri");
            if (string.IsNullOrWhiteSpace(redirectUri)) redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? "";
            if (string.IsNullOrWhiteSpace(redirectUri)) redirectUri = Env("IntuitAuth__RedirectUri");
            if (string.IsNullOrWhiteSpace(redirectUri)) redirectUri = _cfg["IntuitAuth:RedirectUri"] ?? "";

            string scopes     = Env("IntuitPayrollAuth__Scopes");
            if (string.IsNullOrWhiteSpace(scopes)) scopes = _cfg["IntuitPayrollAuth:Scopes"] ?? "";
            if (string.IsNullOrWhiteSpace(scopes)) scopes = Env("IntuitAuth__Scopes");
            if (string.IsNullOrWhiteSpace(scopes)) scopes = _cfg["IntuitAuth:Scopes"] ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return BadRequest("QBO Payroll auth no está configurado (ClientId/RedirectUri).");

            // Intuit usa este endpoint para iniciar OAuth2
            string authBase = "https://appcenter.intuit.com/connect/oauth2";

            var stateObj = new {
                area      = "Payroll",
                companyId = companyId,
                returnTo  = string.IsNullOrWhiteSpace(returnTo) ? $"/Payroll/Empresas/Config/{companyId}" : returnTo
            };
            string state = JsonSerializer.Serialize(stateObj);

            string url = $"{authBase}?client_id={Uri.EscapeDataString(clientId)}" +
                         $"&response_type=code" +
                         $"&scope={Uri.EscapeDataString(scopes)}" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&state={Uri.EscapeDataString(state)}";

            return Redirect(url);
        }
    }
}

