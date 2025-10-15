﻿using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IvaFacilitador.Payroll.Services
{
    public class PayrollAuthService : IPayrollAuthService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private readonly IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext _db;

        public PayrollAuthService(
            IHttpClientFactory http,
            IConfiguration cfg,
            IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db)
        {
            _http = http;
            _cfg  = cfg;
            _db   = db;
        }

        private string IntuitTokenUrl =>
            "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

        private string ApiBase() =>
            (_cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production")
            .Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

        private (string clientId, string clientSecret) GetClientCreds()
        {
            var id  = _cfg["IntuitPayrollAuth:ClientId"]    ?? _cfg["IntuitPayrollAuth__ClientId"];
            var sec = _cfg["IntuitPayrollAuth:ClientSecret"]?? _cfg["IntuitPayrollAuth__ClientSecret"];
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(sec))
                throw new InvalidOperationException("Faltan ClientId/ClientSecret de Intuit (Payroll).");
            return (id!, sec!);
        }

        // ======== PUBLIC API ========

        public async Task<(string realmId, string accessToken)> GetRealmAndValidAccessTokenAsync(
            int companyId,
            CancellationToken ct = default)
        {
            var tok = await _db.PayrollQboTokens
                .Where(t => t.CompanyId == companyId)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (tok == null)
                throw new InvalidOperationException("No hay tokens de Payroll para esta empresa.");

            // ¿expirado o por expirar? refrescar
            var now = DateTimeOffset.UtcNow;
            if (tok.ExpiresAtUtc <= now.AddMinutes(2))
            {
                var refreshed = await RefreshAsync(tok.RefreshToken, ct);
                tok.AccessToken   = refreshed.accessToken;
                tok.RefreshToken  = refreshed.refreshToken;
                tok.ExpiresAtUtc = refreshed.expiresAtUtc.UtcDateTime;
                await _db.SaveChangesAsync(ct);
            }

            if (string.IsNullOrWhiteSpace(tok.RealmId))
                throw new InvalidOperationException("Falta RealmId en los tokens de Payroll.");

            return (tok.RealmId!, tok.AccessToken);
        }

        public async Task<(string? realmId, string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)>
            ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
        {
            var (clientId, clientSecret) = GetClientCreds();
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));

            var client = _http.CreateClient("intuit");
            using var req = new HttpRequestMessage(HttpMethod.Post, IntuitTokenUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",  "authorization_code"),
                new KeyValuePair<string,string>("code",        code),
                new KeyValuePair<string,string>("redirect_uri",redirectUri)
            });

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc    = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            string accessToken  = root.GetProperty("access_token").GetString() ?? "";
            string refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
            int    expiresIn    = root.GetProperty("expires_in").GetInt32();
            var    expiresAt    = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            // En el intercambio de tokens, Intuit NO devuelve realmId.
            // Si el backend que te llama nos lo pasa aparte, va en SaveTokensAsync.
            string? realmId = root.TryGetProperty("realmId", out var r) ? r.GetString() : null;

            return (realmId, accessToken, refreshToken, expiresAt);
        }

        public async Task SaveTokensAsync(
            int companyId,
            string? realmId,
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAtUtc,
            CancellationToken ct = default)
        {
            var entity = new PayrollQboToken
            {
                CompanyId   = companyId,
                RealmId     = realmId,
                AccessToken = accessToken,
                RefreshToken= refreshToken,
                ExpiresAtUtc = expiresAtUtc.UtcDateTime
            };

            _db.PayrollQboTokens.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<string?> TryGetCompanyNameAsync(
            string realmId,
            string accessToken,
            CancellationToken ct = default)
        {
            try
            {
                var url = $"{ApiBase()}/v3/company/{realmId}/companyinfo/{realmId}?minorversion=65";
                var client = _http.CreateClient("intuit");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                using var res = await client.GetAsync(url, ct);
                res.EnsureSuccessStatusCode();

                using var s   = await res.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
                var root = doc.RootElement;

                if (root.TryGetProperty("CompanyInfo", out var ci) && ci.ValueKind == JsonValueKind.Object)
                {
                    if (ci.TryGetProperty("CompanyName", out var cn) && cn.ValueKind == JsonValueKind.String)
                        return cn.GetString();
                    if (ci.TryGetProperty("LegalName", out var ln) && ln.ValueKind == JsonValueKind.String)
                        return ln.GetString();
                }

                // Fallback por SQL
                var qUrl = $"{ApiBase()}/v3/company/{realmId}/query?query={Uri.EscapeDataString("select CompanyName, LegalName from CompanyInfo")}&minorversion=65";
                using var res2 = await client.GetAsync(qUrl, ct);
                if (!res2.IsSuccessStatusCode) return null;

                using var s2   = await res2.Content.ReadAsStreamAsync(ct);
                using var doc2 = await JsonDocument.ParseAsync(s2, cancellationToken: ct);
                var root2 = doc2.RootElement;
                if (root2.TryGetProperty("QueryResponse", out var qr) && qr.ValueKind == JsonValueKind.Object)
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
            catch
            {
                // tolerante a fallos
            }
            return null;
        }

        // ======== Helpers ========

        private async Task<(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc)>
            RefreshAsync(string refreshToken, CancellationToken ct)
        {
            var (clientId, clientSecret) = GetClientCreds();
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));

            var client = _http.CreateClient("intuit");
            using var req = new HttpRequestMessage(HttpMethod.Post, IntuitTokenUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type","refresh_token"),
                new KeyValuePair<string,string>("refresh_token", refreshToken)
            });

            using var res = await client.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc    = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            string newAccess  = root.GetProperty("access_token").GetString() ?? "";
            string newRefresh = root.GetProperty("refresh_token").GetString() ?? refreshToken;
            int    expiresIn  = root.GetProperty("expires_in").GetInt32();

            return (newAccess, newRefresh, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        }
    }
}
