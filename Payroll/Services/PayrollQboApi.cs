using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;


using Microsoft.Extensions.Configuration;
namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollQboApi
    {
        Task<List<QboAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<List<QboItem>>    GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<string?>          GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
    }

    public class QboAccount
    {
        public string? Id { get; set; }
        public string? Name { get; set; } // etiqueta para UI
        public string? Number { get; set; } // AcctNum
        public string? FullyQualifiedName { get; set; }
    }

    public class QboItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
public class PayrollQboApi : IPayrollQboApi
{
    private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _http;

        public PayrollQboApi(IHttpClientFactory http, IConfiguration cfg){
            _http = http;
        _cfg = cfg;
        }

        private HttpClient CreateClient(string accessToken)
        {
            var c = _http.CreateClient("intuit");
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return c;
        }

        public async Task<List<QboAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            using var client = CreateClient(accessToken);
            var baseUrl = ((_cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase) ? "https://sandbox-quickbooks.api.intuit.com" : "https://quickbooks.api.intuit.com"); var url = $"{baseUrl}/v3/company/{realmId}/query?minorversion=65";

            // Traemos también AcctNum y FullyQualifiedName para formar la etiqueta visible en UI.
            var query = "select Id, Name, FullyQualifiedName, AcctNum from Account where Active = true order by FullyQualifiedName";
            var content = new StringContent(query, Encoding.UTF8, "text/plain");

            using var resp = await client.PostAsync(url, content, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var list = new List<QboAccount>();
            if (root.TryGetProperty("QueryResponse", out var q) && q.TryGetProperty("Account", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id   = el.TryGetProperty("Id", out var jid)   ? jid.GetString()   : null;
                    var name = el.TryGetProperty("Name", out var jnm) ? jnm.GetString()   : null;
                    var fqn  = el.TryGetProperty("FullyQualifiedName", out var jfqn) ? jfqn.GetString() : null;
                    var num  = el.TryGetProperty("AcctNum", out var jnum) ? jnum.GetString() : null;

                    // Etiqueta compuesta: "62011 GASTOS...:NombreVisible"
                    string label;
                    if (!string.IsNullOrWhiteSpace(num) && !string.IsNullOrWhiteSpace(fqn) && !string.IsNullOrWhiteSpace(name))
                        label = $"{num} {fqn}: {name}";
                    else if (!string.IsNullOrWhiteSpace(fqn) && !string.IsNullOrWhiteSpace(name))
                        label = $"{fqn}: {name}";
                    else
                        label = name ?? fqn ?? id ?? "(sin nombre)";

                    list.Add(new QboAccount
                    {
                        Id = id,
                        Name = label,
                        Number = num,
                        FullyQualifiedName = fqn
                    });
                }
            }

            return list;
        }

        public async Task<List<QboItem>> GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            using var client = CreateClient(accessToken);
            var baseUrl = ((_cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase) ? "https://sandbox-quickbooks.api.intuit.com" : "https://quickbooks.api.intuit.com"); var url = $"{baseUrl}/v3/company/{realmId}/query?minorversion=65";
            var query = "select Id, Name from Item where Active = true and Type in ('Service') order by Name";
            var content = new StringContent(query, Encoding.UTF8, "text/plain");

            using var resp = await client.PostAsync(url, content, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var list = new List<QboItem>();
            if (root.TryGetProperty("QueryResponse", out var q) && q.TryGetProperty("Item", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    list.Add(new QboItem
                    {
                        Id = el.TryGetProperty("Id", out var jid) ? jid.GetString() : null,
                        Name = el.TryGetProperty("Name", out var jn) ? jn.GetString() : null
                    });
                }
            }

            return list;
        }

        public async Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            using var client = CreateClient(accessToken);
            var baseUrl = ((_cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase) ? "https://sandbox-quickbooks.api.intuit.com" : "https://quickbooks.api.intuit.com"); var url = $"{baseUrl}/v3/company/{realmId}/companyinfo/{realmId}?minorversion=65";

            using var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("CompanyInfo", out var ci))
            {
                if (ci.TryGetProperty("CompanyName", out var cn)) return cn.GetString();
                if (ci.TryGetProperty("LegalName", out var ln)) return ln.GetString();
            }
            return null;
        }
    }
}





