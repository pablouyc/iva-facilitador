using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net.Http;

namespace IvaFacilitador.Payroll.Services
{
    public partial class PayrollQboApi : IPayrollQboApi
    {
        public async Task<List<(string Id,string Name)>> GetEmployeesAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            using var client = CreateClient(accessToken);

            var env = _cfg["IntuitPayrollAuth:Environment"] ?? _cfg["IntuitPayrollAuth__Environment"] ?? "production";
            var baseUrl = env.Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var sql = "select Id, DisplayName, GivenName, FamilyName from Employee where Active = true order by DisplayName";
            var postUrl = $"{baseUrl}/v3/company/{realmId}/query?minorversion=65";

            // 1) Intento por POST text/plain
            using var postRes = await client.PostAsync(postUrl, new StringContent(sql, Encoding.UTF8, "text/plain"), ct);
            var body = await postRes.Content.ReadAsStringAsync(ct);

            if (!postRes.IsSuccessStatusCode)
            {
                // Si QBO responde 400 (parser), probamos GET con query codificada
                if ((int)postRes.StatusCode == 400)
                {
                    var getUrl = $"{baseUrl}/v3/company/{realmId}/query?query={Uri.EscapeDataString(sql)}&minorversion=65";
                    using var getRes = await client.GetAsync(getUrl, ct);
                    body = await getRes.Content.ReadAsStringAsync(ct);
                    if (!getRes.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[QBO][employees-list] {(int)getRes.StatusCode} {getRes.ReasonPhrase} body: {body}");
                        getRes.EnsureSuccessStatusCode();
                    }
                    return ParseEmployees(body);
                }

                Console.WriteLine($"[QBO][employees-list] {(int)postRes.StatusCode} {postRes.ReasonPhrase} body: {body}");
                postRes.EnsureSuccessStatusCode();
            }

            return ParseEmployees(body);
        }

        private static List<(string Id,string Name)> ParseEmployees(string payload)
        {
            var list = new List<(string Id,string Name)>();

            // JSON
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("QueryResponse", out var qr) &&
                    qr.TryGetProperty("Employee", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var id = el.TryGetProperty("Id", out var jid) ? jid.GetString() : null;
                        var dn = el.TryGetProperty("DisplayName", out var jdn) ? jdn.GetString() : null;
                        var gn = el.TryGetProperty("GivenName", out var jgn) ? jgn.GetString() : null;
                        var fn = el.TryGetProperty("FamilyName", out var jfn) ? jfn.GetString() : null;
                        var name = !string.IsNullOrWhiteSpace(dn)
                            ? dn
                            : string.Join(" ", new[] { gn, fn }.Where(s => !string.IsNullOrWhiteSpace(s)));

                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                            list.Add((id!, name!));
                    }
                }
            }
            catch { /* no json */ }

            // XML
            if (list.Count == 0)
            {
                try
                {
                    var x = XDocument.Parse(payload);
                    var emps = x.Descendants().Where(e => e.Name.LocalName == "Employee");
                    foreach (var e in emps)
                    {
                        var id = e.Descendants().FirstOrDefault(n => n.Name.LocalName == "Id")?.Value;
                        var dn = e.Descendants().FirstOrDefault(n => n.Name.LocalName == "DisplayName")?.Value;
                        var gn = e.Descendants().FirstOrDefault(n => n.Name.LocalName == "GivenName")?.Value;
                        var fn = e.Descendants().FirstOrDefault(n => n.Name.LocalName == "FamilyName")?.Value;
                        var name = !string.IsNullOrWhiteSpace(dn)
                            ? dn
                            : string.Join(" ", new[] { gn, fn }.Where(s => !string.IsNullOrWhiteSpace(s)));

                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                            list.Add((id!, name!));
                    }
                }
                catch { /* no xml */ }
            }

            return list;
        }
    }
}
