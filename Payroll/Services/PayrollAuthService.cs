using IvaFacilitador.Areas.Payroll.ModelosPayroll;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace IvaFacilitador.Payroll.Services
{
    public class PayrollAuthService : IPayrollAuthService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private readonly IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext _db;

        public PayrollAuthService(IHttpClientFactory http, IConfiguration cfg, IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db)
        {
            _http = http;
            _cfg  = cfg;
            _db   = db;
        }

        public string GetAuthorizeUrl(int companyId, string returnTo)
        {
            var clientId    = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var redirectUri = _cfg["IntuitPayrollAuth:RedirectUri"] ?? _cfg["IntuitPayrollAuth__RedirectUri"];
            var scopes      = _cfg["IntuitPayrollAuth:Scopes"]      ?? _cfg["IntuitPayrollAuth__Scopes"] ?? "com.intuit.quickbooks.accounting";
            var env         = _cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production";

            var authBase = "https://appcenter.intuit.com/connect/oauth2";
            var stateObj = new { area = "Payroll", returnTo, companyId, nonce = Guid.NewGuid().ToString("N") };
            var stateB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stateObj)));

            var qs = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"]     = clientId,
                ["redirect_uri"]  = redirectUri,
                ["response_type"] = "code",
                ["scope"]         = scopes,
                ["state"]         = stateB64
            });

            return authBase + qs.Value;
        }

        public async Task<(string accessToken, string refreshToken, DateTime expiresAtUtc, string? realmId)>
            ExchangeCodeAsync(string code, string redirectUri)
        {
            var clientId     = _cfg["IntuitPayrollAuth:ClientId"]     ?? _cfg["IntuitPayrollAuth__ClientId"];
            var clientSecret = _cfg["IntuitPayrollAuth:ClientSecret"] ?? _cfg["IntuitPayrollAuth__ClientSecret"];
            var tokenEndpoint = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

            var client = _http.CreateClient("intuit");
            var basic  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]  = "authorization_code",
                ["code"]        = code,
                ["redirect_uri"]= redirectUri
            });

            using var resp = await client.PostAsync(tokenEndpoint, content);
            resp.EnsureSuccessStatusCode();

            var payload = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var access  = root.GetProperty("access_token").GetString();
            var refresh = root.GetProperty("refresh_token").GetString();
            var expires = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;
            var realmId = root.TryGetProperty("realmId", out var r) ? r.GetString() : null;

            return (access!, refresh!, DateTime.UtcNow.AddSeconds(expires), realmId);
        }

        public async Task SaveTokensAsync(int companyId, string? realmId, string accessToken, string refreshToken, DateTime expiresAtUtc)
        {
            var row = new PayrollQboToken
            {
                CompanyId    = companyId,
                RealmId      = realmId,
                AccessToken  = accessToken,
                RefreshToken = refreshToken,
                TokenType    = "Bearer",
                Scope        = null,
                ExpiresAtUtc = expiresAtUtc
            };

            _db.PayrollQboTokens.Add(row);
            await _db.SaveChangesAsync();
        }

        public async Task<string?> TryGetCompanyNameAsync(string realmId, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(realmId) || string.IsNullOrWhiteSpace(accessToken))
                return null;

            var client = _http.CreateClient("intuit");
            if (client.BaseAddress == null)
                client.BaseAddress = new Uri("https://quickbooks.api.intuit.com/");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"v3/company/{Uri.EscapeDataString(realmId)}/companyinfo/{Uri.EscapeDataString(realmId)}?minorversion=65";
            using var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("CompanyInfo", out var ci))
                {
                    if (ci.TryGetProperty("CompanyName", out var cn) && cn.ValueKind == JsonValueKind.String)
                        return cn.GetString();
                    if (ci.TryGetProperty("LegalName", out var ln) && ln.ValueKind == JsonValueKind.String)
                        return ln.GetString();
                }

                if (root.TryGetProperty("QueryResponse", out var qr) && qr.ValueKind == JsonValueKind.Object)
                {
                    if (qr.TryGetProperty("CompanyInfo", out var ci2) && ci2.ValueKind == JsonValueKind.Array && ci2.GetArrayLength() > 0)
                    {
                        var item = ci2[0];
                        if (item.TryGetProperty("CompanyName", out var cn2) && cn2.ValueKind == JsonValueKind.String)
                            return cn2.GetString();
                        if (item.TryGetProperty("LegalName", out var ln2) && ln2.ValueKind == JsonValueKind.String)
                            return ln2.GetString();
                    }
                }
            }
            catch { }

            return null;
        }
    }
}

