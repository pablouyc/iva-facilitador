using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;
namespace IvaFacilitador.Payroll.Services
{
    public class PayrollAuthService : IPayrollAuthService
    {
        private readonly IConfiguration _cfg;
        private readonly PayrollDbContext _db;
        private readonly IHttpClientFactory _http;

        private static readonly Uri AuthBase = new("https://appcenter.intuit.com/connect/oauth2");
        private static readonly Uri TokenUrl = new("https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");

        public PayrollAuthService(IConfiguration cfg, PayrollDbContext db, IHttpClientFactory http)
        {
            _cfg = cfg;
            _db  = db;
            _http = http;
        }

        private string? C(string k) => _cfg[k];
        private string EnvOrCfg(string env, string cfg) =>
            Environment.GetEnvironmentVariable(env) ?? (C(cfg) ?? string.Empty);

        public string GetAuthorizeUrl(int companyId, string returnTo)
        {
            var clientId    = EnvOrCfg("IntuitPayrollAuth__ClientId",     "IntuitPayrollAuth:ClientId");
            var scopes      = EnvOrCfg("IntuitPayrollAuth__Scopes",       "IntuitPayrollAuth:Scopes");
            var redirectUri = EnvOrCfg("IntuitPayrollAuth__RedirectUri",  "IntuitPayrollAuth:RedirectUri");

            if (string.IsNullOrWhiteSpace(clientId))    throw new InvalidOperationException("Missing IntuitPayrollAuth ClientId");
            if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Missing IntuitPayrollAuth RedirectUri");

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
            var clientId = EnvOrCfg("IntuitPayrollAuth__ClientId",     "IntuitPayrollAuth:ClientId");
            var secret   = EnvOrCfg("IntuitPayrollAuth__ClientSecret", "IntuitPayrollAuth:ClientSecret");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Missing IntuitPayrollAuth ClientId/ClientSecret");

            var client = _http.CreateClient("intuit");
            var basic  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);

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

            var access    = root.GetProperty("access_token").GetString()!;
            var refresh   = root.GetProperty("refresh_token").GetString()!;
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            var realmId   = root.TryGetProperty("realmId", out var r) ? r.GetString() : null;

            var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60); // colchón de 1 min
            return (access, refresh, expiresAt, realmId);
        }

        public async Task SaveTokensAsync(int companyId, string? realmId, string accessToken, string refreshToken, DateTime expiresAtUtc)
        {
            // Persistimos/actualizamos los tokens
            var tok = _db.PayrollQboTokens.SingleOrDefault(x => x.CompanyId == companyId);
            if (tok == null)
            {
                tok = new PayrollQboToken { CompanyId = companyId };
                _db.PayrollQboTokens.Add(tok);
            }
            tok.RealmId = realmId ?? tok.RealmId;
            tok.AccessToken = accessToken;
            tok.RefreshToken = refreshToken;
            tok.ExpiresAtUtc = expiresAtUtc;
            await _db.SaveChangesAsync();

            // Opción: reflejar realm en Company (si existe esa columna)
            var comp = await _db.Companies.FindAsync(companyId);
            if (comp != null && !string.IsNullOrWhiteSpace(tok.RealmId))
            {
                comp.QboId = tok.RealmId;
                await _db.SaveChangesAsync();
            }
        }
    }
}


