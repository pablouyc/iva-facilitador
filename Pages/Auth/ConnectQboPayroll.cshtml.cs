using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace IvaFacilitador.Pages.Auth
{
    public class ConnectQboPayrollModel : PageModel
    {
        public IActionResult OnGet(string companyId, string returnTo)
        {
            var stateObj = new { companyId = companyId ?? "", returnTo = returnTo ?? "" };
            var stateJson = JsonSerializer.Serialize(stateObj);
            var stateB64  = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

            string? Env(string k) => Environment.GetEnvironmentVariable(k);

            var clientId    = Env("IntuitPayrollAuth__ClientId")    ?? Env("IntuitAuth__ClientId");
            var redirectUri = Env("IntuitPayrollAuth__RedirectUri") ?? Env("IntuitAuth__RedirectUri");
            var scopes      = Env("IntuitPayrollAuth__Scopes")      ?? Env("IntuitAuth__Scopes") ?? "com.intuit.quickbooks.accounting";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return BadRequest("Intuit ClientId/RedirectUri no configurados.");

            var url = $"https://appcenter.intuit.com/connect/oauth2?client_id={Uri.EscapeDataString(clientId)}&response_type=code&scope={Uri.EscapeDataString(scopes)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(stateB64)}";
            return Redirect(url);
        }
    }
}
