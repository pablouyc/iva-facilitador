using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IvaFacilitador.Payroll.Services
{
    public class PayrollAuthService : IPayrollAuthService
    {
        private readonly IConfiguration _cfg;
        public PayrollAuthService(IConfiguration cfg) { _cfg = cfg; }

        private string? C(string k) => _cfg[k];
        private string EnvOrCfg(string env, string cfg) =>
            Environment.GetEnvironmentVariable(env) ?? (C(cfg) ?? "");

        public string GetAuthorizeUrl(string returnTo)
        {
            // Lee SOLO claves de Payroll
            var clientId    = EnvOrCfg("IntuitPayrollAuth__ClientId",    "IntuitPayrollAuth:ClientId");
            var scopes      = EnvOrCfg("IntuitPayrollAuth__Scopes",      "IntuitPayrollAuth:Scopes");
            var redirectUri = EnvOrCfg("IntuitPayrollAuth__RedirectUri", "IntuitPayrollAuth:RedirectUri");

            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Missing IntuitPayrollAuth__ClientId");
            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new InvalidOperationException("Missing IntuitPayrollAuth__RedirectUri");

            // state con retorno y nonce
            var stateObj = new { area = "Payroll", returnTo, nonce = Guid.NewGuid().ToString("N") };
            var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stateObj)));

            var q = new Dictionary<string,string?>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["scope"] = scopes,
                ["redirect_uri"] = redirectUri,
                ["state"] = state
            };
            var qs = string.Join("&", q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? "")}"));
            return $"https://appcenter.intuit.com/connect/oauth2?{qs}";
        }
    }
}
