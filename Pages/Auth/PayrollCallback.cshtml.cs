using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using IvaFacilitador.Payroll.Services;

namespace IvaFacilitador.Pages.Auth
{
    [AllowAnonymous]
    public class PayrollCallbackModel : PageModel
    {
        private readonly IPayrollAuthService _auth;
        private readonly IConfiguration _cfg;

        public PayrollCallbackModel(IPayrollAuthService auth, IConfiguration cfg)
        {
            _auth = auth;
            _cfg = cfg;
        }

        [BindProperty(SupportsGet = true)] public string? code { get; set; }
        [BindProperty(SupportsGet = true)] public string? state { get; set; }
        [BindProperty(SupportsGet = true)] public string? realmId { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return BadRequest("Missing code/state");

            // Decodificar state
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var companyId = root.GetProperty("companyId").GetInt32();
            var returnTo  = root.TryGetProperty("returnTo", out var r) ? (r.GetString() ?? "/Payroll/Empresas") : "/Payroll/Empresas";

            var redirectUri = Environment.GetEnvironmentVariable("IntuitPayrollAuth__RedirectUri")
                              ?? (_cfg["IntuitPayrollAuth:RedirectUri"] ?? "");

            var (access, refresh, exp, realmFromToken) = await _auth.ExchangeCodeAsync(code!, redirectUri);
            var realm = !string.IsNullOrWhiteSpace(realmId) ? realmId : realmFromToken;

            await _auth.SaveTokensAsync(companyId, realm, access, refresh, exp);

            return Redirect(returnTo);
        }
    }
}
