using System.Net.Http.Headers;
using System.Text.Json;

namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollQboApi
    {
        Task<IReadOnlyList<QbAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<IReadOnlyList<QbItem>>    GetServiceItemsAsync   (string realmId, string accessToken, CancellationToken ct = default);

        // NUEVO
        Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
    }

    public class QbAccount { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? AccountType { get; set; } }
    public class QbItem    { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? Type        { get; set; } }

    public class PayrollQboApi : IPayrollQboApi
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private string ApiBase() => (_cfg["IntuitPayrollAuth:Environment"] ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
            ? "https://sandbox-quickbooks.api.intuit.com"
            : "https://quickbooks.api.intuit.com";

        public PayrollQboApi(IHttpClientFactory http, IConfiguration cfg)
        {
            _http = http; _cfg = cfg;
        }

        private async Task<JsonDocument> RunQueryAsync(string realmId, string accessToken, string sql, CancellationToken ct)
        {
            var url = $"{ApiBase()}/v3/company/{realmId}/query?query={Uri.EscapeDataString(sql)}&minorversion=65";
            var client = _http.CreateClient("intuit");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var res = await client.GetAsync(url, ct);
            res.EnsureSuccessStatusCode();
            using var s = await res.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(s, cancellationToken: ct);
        }

        public async Task<IReadOnlyList<QbAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, AccountType from Account where AccountType in ('Expense','Cost of Goods Sold')";
            using var doc = await RunQueryAsync(realmId, accessToken, sql, ct);
            var list = new List<QbAccount>();
            var root = doc.RootElement;
            if (root.TryGetProperty("QueryResponse", out var qr) && qr.TryGetProperty("Account", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    list.Add(new QbAccount
                    {
                        Id = el.GetProperty("Id").GetString() ?? "",
                        Name = el.GetProperty("Name").GetString() ?? "",
                        AccountType = el.TryGetProperty("AccountType", out var at) ? at.GetString() : null
                    });
                }
            }
            return list;
        }

        public async Task<IReadOnlyList<QbItem>> GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var sql = "select Id, Name, Type from Item where Type = 'Service'";
            using var doc = await RunQueryAsync(realmId, accessToken, sql, ct);
            var list = new List<QbItem>();
            var root = doc.RootElement;
            if (root.TryGetProperty("QueryResponse", out var qr) && qr.TryGetProperty("Item", out var arr))
            {
                foreach (var el in arr.EnumerateArray())
                {
                    list.Add(new QbItem
                    {
                        Id = el.GetProperty("Id").GetString() ?? "",
                        Name = el.GetProperty("Name").GetString() ?? "",
                        Type = el.TryGetProperty("Type", out var tp) ? tp.GetString() : null
                    });
                }
            }
            return list;
        }

        // === NUEVO ===
        public async Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            // QBO soporta CompanyInfo por consulta
            var sql = "select CompanyName, LegalName from CompanyInfo";
            using var doc = await RunQueryAsync(realmId, accessToken, sql, ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("QueryResponse", out var qr) && qr.TryGetProperty("CompanyInfo", out var arr))
            {
                var first = arr.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined)
                {
                    var companyName = first.TryGetProperty("CompanyName", out var cn) ? cn.GetString() : null;
                    var legalName   = first.TryGetProperty("LegalName",   out var ln) ? ln.GetString() : null;
                    return !string.IsNullOrWhiteSpace(companyName) ? companyName : legalName;
                }
            }
            return null;
        }
    }
}
