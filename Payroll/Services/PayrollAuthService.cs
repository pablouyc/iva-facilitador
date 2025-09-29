using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollAuthService
    {
        string GetAuthorizeUrl(int companyId, string returnTo);
        Task<(string accessToken, string refreshToken, DateTime expiresAtUtc, string? realmId)> ExchangeCodeAsync(string code, string redirectUri);
        Task SaveTokensAsync(int companyId, string? realmId, string accessToken, string refreshToken, DateTime expiresAtUtc);
    }

    public class PayrollAuthService : IPayrollAuthService
    {
        private readonly IConfiguration _cfg;
        private readonly IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext _db;
        private readonly IHttpClientFactory _http;

        private static readonly Uri AuthBase = new("https://appcenter.intuit.com/connect/oauth2");
        private static readonly Uri TokenUrl = new("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");

        public PayrollAuthService(IConfiguration cfg, IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db, IHttpClientFactory http)
        {
            _cfg = cfg;
            _db  = db;
            _http = http;
        }

        private string? C(string k) => _cfg[k];
        private string EnvOrCfg(string env, string cfg) =>
            Environment.GetEnvironmentVariable(env) ?? (C(cfg) ?? "");

        public string GetAuthorizeUrl(int companyId, string returnTo)
        {
            var clientId    = EnvOrCfg("IntuitPayrollAuth__ClientId",     "IntuitPayrollAuth:ClientId");
            var scopes      = EnvOrCfg("IntuitPayrollAuth__Scopes",       "IntuitPayrollAuth:Scopes");
            var redirectUri = EnvOrCfg("IntuitPayrollAuth__RedirectUri",  "IntuitPayrollAuth:RedirectUri");
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Missing IntuitPayrollAuth ClientId");
            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new InvalidOperationException("Missing IntuitPayrollAuth RedirectUri");

            var stateObj = new {
                companyId,
                returnTo,
                area = "Payroll",
                nonce = Guid.NewGuid().ToString("N")
            };
            var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stateObj)));

            var q = new Dictionary<string,string>{
                ["client_id"]     = clientId,
                ["response_type"] = "code",
                ["scope"]         = scopes,
                ["redirect_uri"]  = redirectUri,
                ["state"]         = state
            };

            var qs = string.Join("&", q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? "")}"));
            return $"{AuthBase}?{qs}";
        }

        public async Task<(string accessToken, string refreshToken, DateTime expiresAtUtc, string? realmId)> ExchangeCodeAsync(string code, string redirectUri)
        {
            var clientId = EnvOrCfg("IntuitPayrollAuth__ClientId", "IntuitPayrollAuth:ClientId");
            var secret   = EnvOrCfg("IntuitPayrollAuth__ClientSecret", "IntuitPayrollAuth:ClientSecret");
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Missing IntuitPayrollAuth ClientId/Secret");

            var client = _http.CreateClient("intuit");
            var basic  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);

            var form = new Dictionary<string,string>{
                ["grant_type"]   = "authorization_code",
                ["code"]         = code,
                ["redirect_uri"] = redirectUri
            };
            var resp = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var access   = root.GetProperty("access_token").GetString()!;
            var refresh  = root.GetProperty("refresh_token").GetString()!;
            var expiresIn= root.GetProperty("expires_in").GetInt32(); // seconds
            var realmId  = root.TryGetProperty("realmId", out var r) ? r.GetString() : null;

            var expires = DateTime.UtcNow.AddSeconds(expiresIn - 60); // margen
            return (access, refresh, expires, realmId);
        }

        public async Task SaveTokensAsync(int companyId, string? realmId, string accessToken, string refreshToken, DateTime expiresAtUtc)
        {
            var tok = _db.PayrollQboTokens.SingleOrDefault(x => x.CompanyId == companyId);
            if (tok == null)
            {
                tok = new IvaFacilitador.Areas.Payroll.ModelosPayroll.PayrollQboToken { CompanyId = companyId };
                _db.PayrollQboTokens.Add(tok);
            }
            tok.RealmId = realmId ?? tok.RealmId;
            tok.AccessToken = accessToken;
            tok.RefreshToken = refreshToken;
            tok.ExpiresAtUtc = expiresAtUtc;
            await _db.SaveChangesAsync();

            var comp = await _db.Companies.FindAsync(companyId);
            if (comp != null && !string.IsNullOrWhiteSpace(tok.RealmId))
            {
                comp.QboId = tok.RealmId;
                await _db.SaveChangesAsync();
            }
        }
    }
}








