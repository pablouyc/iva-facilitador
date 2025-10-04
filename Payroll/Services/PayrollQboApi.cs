using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollQboApi
    {
        Task<IReadOnlyList<QbAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<IReadOnlyList<QbItem>>    GetServiceItemsAsync   (string realmId, string accessToken, CancellationToken ct = default);
    }

    public record QbAccount(string Id, string Name, string? AccountType);
    public record QbItem   (string Id, string Name, string? Type);

    public class PayrollQboApi : IPayrollQboApi
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<PayrollQboApi> _log;
        private readonly IConfiguration _cfg;

        public PayrollQboApi(IHttpClientFactory http, ILogger<PayrollQboApi> log, IConfiguration cfg)
        {
            _http = http;
            _log  = log;
            _cfg  = cfg;
        }

        private string ApiBase()
        {
            var env = _cfg["IntuitPayrollAuth:Environment"] ?? "production";
            return env.Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";
        }

        private async Task<JsonDocument> QueryAsync(string realmId, string sql, string accessToken, CancellationToken ct)
        {
            var url = $"{ApiBase()}/v3/company/{realmId}/query?query={Uri.EscapeDataString(sql)}&minorversion=65";
            var client = _http.CreateClient("intuit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("QBO Query HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                throw new InvalidOperationException($"QBO query failed {(int)resp.StatusCode}");
            }
            return JsonDocument.Parse(body);
        }

        public async Task<IReadOnlyList<QbAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, AccountType from Account where AccountType in ('Expense','Cost of Goods Sold')";
            using var doc = await QueryAsync(realmId, sql, accessToken, ct);
            var list = new List<QbAccount>();
            var root = doc.RootElement;
            if (root.TryGetProperty("QueryResponse", out var qr) && qr.TryGetProperty("Account", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id   = el.GetProperty("Id").GetString() ?? "";
                    var name = el.GetProperty("Name").GetString() ?? "";
                    var type = el.TryGetProperty("AccountType", out var t) ? t.GetString() : null;
                    list.Add(new QbAccount(id, name, type));
                }
            }
            return list;
        }

        public async Task<IReadOnlyList<QbItem>> GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, Type from Item where Type = 'Service'";
            using var doc = await QueryAsync(realmId, sql, accessToken, ct);
            var list = new List<QbItem>();
            var root = doc.RootElement;
            if (root.TryGetProperty("QueryResponse", out var qr) && qr.TryGetProperty("Item", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id   = el.GetProperty("Id").GetString() ?? "";
                    var name = el.GetProperty("Name").GetString() ?? "";
                    var type = el.TryGetProperty("Type", out var t) ? t.GetString() : null;
                    list.Add(new QbItem(id, name, type));
                }
            }
            return list;
        }
    }
}