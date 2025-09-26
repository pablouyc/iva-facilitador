using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;

namespace IvaFacilitador.Pages.Auth
{
    public class ConnectQboPayrollModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public ConnectQboPayrollModel(IConfiguration cfg) { _cfg = cfg; }

        public IActionResult OnGet(int companyId, string? returnTo = null)
        {
            // *** SOLO RRHH ***
            string? cid         = _cfg["IntuitPayrollAuth:ClientId"]     ?? Environment.GetEnvironmentVariable("IntuitPayrollAuth__ClientId");
            string? scopes      = _cfg["IntuitPayrollAuth:Scopes"]        ?? Environment.GetEnvironmentVariable("IntuitPayrollAuth__Scopes") ?? "com.intuit.quickbooks.accounting";
            string? redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"]   ?? Environment.GetEnvironmentVariable("IntuitPayrollAuth__RedirectUri");
            if (string.IsNullOrWhiteSpace(redirectUri))
                redirectUri = $"{Request.Scheme}://{Request.Host}/Auth/Callback"; // fallback seguro
            if (string.IsNullOrWhiteSpace(cid))
                return BadRequest("Missing IntuitPayrollAuth ClientId.");

            var stateObj = new {
                companyId = companyId,
                returnTo  = returnTo ?? $"/Payroll/Empresas/Config/{companyId}",
                tag       = "payroll"
            };
            string state = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stateObj)));

            string baseUrl = "https://appcenter.intuit.com/connect/oauth2";
            string url = baseUrl
                + "?client_id="    + Uri.EscapeDataString(cid)
                + "&response_type=code"
                + "&scope="        + Uri.EscapeDataString(scopes)
                + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
                + "&state="        + Uri.EscapeDataString(state);

            return Redirect(url);
        }
    }
}

