// Payroll/Services/PayrollQboApi.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollQboApi
    {
        // Mantengo el nombre por compatibilidad: devuelve TODAS las cuentas activas.
        Task<List<QboAccount>> GetExpenseAccountsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<List<QboItem>>    GetServiceItemsAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<string?>          GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
    }

    public sealed class PayrollQboApi : IPayrollQboApi
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<PayrollQboApi> _log;

        public PayrollQboApi(IHttpClientFactory httpFactory, ILogger<PayrollQboApi> log)
        {
            _httpFactory = httpFactory;
            _log         = log;
        }

        private HttpClient CreateClient(string accessToken)
        {
            var c = _httpFactory.CreateClient("intuit");
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            c.DefaultRequestHeaders.Accept.Clear();
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return c;
        }

        private static string BuildQueryUrl(string realmId) =>
            $"https://quickbooks.api.intuit.com/v3/company/{realmId}/query?minorversion=65";

        private static StringContent QboContent(string query) =>
            new StringContent(query, Encoding.UTF8, "text/plain");

        /// <summary>
        /// Devuelve **todas** las cuentas activas (sin filtrar por tipo).
        /// Si la cuenta tiene número (AcctNum), el Name resultante será:
        /// "{AcctNum} {FullyQualifiedName}". Si no, usa FullyQualifiedName o Name.
        /// </summary>
        public async Task<List<QboAccount>> GetExpenseAccountsAsync(
            string realmId,
            string accessToken,
            CancellationToken ct = default)
        {
            var query =
                "select Id, Name, FullyQualifiedName, AcctNum " +
                "from Account " +
                "where Active = true " +
                "order by FullyQualifiedName";

            var url  = BuildQueryUrl(realmId);
            var http = CreateClient(accessToken);

            using var content = QboContent(query);
            using var resp    = await http.PostAsync(url, content, ct);
            var json          = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("QBO GetAccounts failed: {Status} {Body}", resp.StatusCode, json);
                resp.EnsureSuccessStatusCode();
            }

            var list = new List<QboAccount>();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                qr.TryGetProperty("Account", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id = el.GetProperty("Id").GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    var fqn = el.TryGetProperty("FullyQualifiedName", out var fqnEl) ? fqnEl.GetString() ?? "" : "";
                    var nm  = el.TryGetProperty("Name", out var nEl) ? nEl.GetString() ?? "" : "";
                    var num = el.TryGetProperty("AcctNum", out var numEl) ? numEl.GetString() ?? "" : "";

                    var baseName = !string.IsNullOrWhiteSpace(fqn) ? fqn : nm;
                    var display  = !string.IsNullOrWhiteSpace(num) ? $"{num} {baseName}" : baseName;

                    list.Add(new QboAccount
                    {
                        Id = id,
                        Name = display,
                        AcctNum = num,
                        FullyQualifiedName = baseName
                    });
                }
            }

            return list
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<QboItem>> GetServiceItemsAsync(
            string realmId,
            string accessToken,
            CancellationToken ct = default)
        {
            var query =
                "select Id, Name " +
                "from Item " +
                "where Type = 'Service' and Active = true " +
                "order by Name";

            var url  = BuildQueryUrl(realmId);
            var http = CreateClient(accessToken);

            using var content = QboContent(query);
            using var resp    = await http.PostAsync(url, content, ct);
            var json          = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("QBO GetServiceItems failed: {Status} {Body}", resp.StatusCode, json);
                resp.EnsureSuccessStatusCode();
            }

            var list = new List<QboItem>();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                qr.TryGetProperty("Item", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var id = el.GetProperty("Id").GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    var name = el.TryGetProperty("Name", out var n) ? (n.GetString() ?? "") : "";
                    list.Add(new QboItem { Id = id, Name = name });
                }
            }

            return list
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<string?> GetCompanyNameAsync(
            string realmId,
            string accessToken,
            CancellationToken ct = default)
        {
            var query = "select CompanyName from CompanyInfo";
            var url   = BuildQueryUrl(realmId);
            var http  = CreateClient(accessToken);

            using var content = QboContent(query);
            using var resp    = await http.PostAsync(url, content, ct);
            var json          = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("QBO CompanyInfo failed: {Status} {Body}", resp.StatusCode, json);
                resp.EnsureSuccessStatusCode();
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                qr.TryGetProperty("CompanyInfo", out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                var first = arr.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined &&
                    first.TryGetProperty("CompanyName", out var cn))
                {
                    return cn.GetString();
                }
            }
            return null;
        }
    }

    public sealed class QboAccount
    {
        public string? Id { get; set; }
        /// <summary>Texto para mostrar en la UI (p.ej. "62011 Gastos …").</summary>
        public string? Name { get; set; }
        /// <summary>Número de cuenta (si existe en QBO).</summary>
        public string? AcctNum { get; set; }
        /// <summary>Nombre completo jerárquico (QBO: FullyQualifiedName).</summary>
        public string? FullyQualifiedName { get; set; }
    }

    public sealed class QboItem
    {
        public string? Id   { get; set; }
        public string? Name { get; set; }
    }
}

