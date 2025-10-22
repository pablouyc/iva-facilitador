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
            var url = $"https://quickbooks.api.intuit.com/v3/company/{realmId}/query?minorversion=65";
            var query = "select Id, DisplayName, GivenName, FamilyName from Employee where Active = true order by DisplayName";
            using var res = await client.PostAsync(url, new StringContent(query, Encoding.UTF8, "text/plain"), ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[QBO][employees-list] {(int)res.StatusCode} {res.ReasonPhrase} body: {body}");
                res.EnsureSuccessStatusCode();
            }

            var list = new List<(string Id,string Name)>();

            // JSON
            try
            {
                using var doc = JsonDocument.Parse(body);
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
                    var x = XDocument.Parse(body);
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


