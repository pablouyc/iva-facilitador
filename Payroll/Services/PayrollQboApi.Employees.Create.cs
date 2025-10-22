using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IvaFacilitador.Payroll.Services
{
    public partial class PayrollQboApi : IPayrollQboApi
    {
        // Busca por DisplayName y, si no existe, lo crea en QBO y devuelve el Id
        public async Task<string> CreateOrLinkEmployeeAsync(
            string realmId,
            string accessToken,
            string displayName,
            string? givenName = null,
            string? familyName = null,
            CancellationToken ct = default)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            // 1) Intentar encontrar por DisplayName
            var safeName = displayName?.Replace("'", "''") ?? "";
            var q = $"select Id, DisplayName from Employee where DisplayName = '{safeName}'";
            var qUrl = $"https://quickbooks.api.intuit.com/v3/company/{realmId}/query?minorversion=65";
            using (var qRes = await client.PostAsync(qUrl, new StringContent(q, Encoding.UTF8, "text/plain"), ct))
            {
                if (qRes.IsSuccessStatusCode)
                {
                    var qPayload = await qRes.Content.ReadAsStringAsync(ct);
                    var existingId = TryExtractEmployeeId(qPayload);
                    if (!string.IsNullOrEmpty(existingId))
                        return existingId!;
                }
            }

            // 2) Crear si no existe
            var createUrl = $"https://quickbooks.api.intuit.com/v3/company/{realmId}/employee?minorversion=65";
            var bodyObj = new Dictionary<string, object?>
            {
                ["DisplayName"] = displayName,
                ["GivenName"]   = givenName   ?? displayName,
                ["FamilyName"]  = familyName  ?? displayName
            };
            var json = JsonSerializer.Serialize(bodyObj);
            using var cRes = await client.PostAsync(createUrl, new StringContent(json, Encoding.UTF8, "application/json"), ct);
            cRes.EnsureSuccessStatusCode();
            var payload = await cRes.Content.ReadAsStringAsync(ct);
            var newId = TryExtractEmployeeId(payload);
            if (!string.IsNullOrEmpty(newId)) return newId!;
            throw new System.InvalidOperationException("No se pudo obtener el Id del empleado desde la respuesta de QBO.");
        }

        // Soporta respuestas JSON y XML tanto de query como de create
        private static string? TryExtractEmployeeId(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;

            // JSON
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                // { "QueryResponse": { "Employee": [ { "Id": "123" } ] } }
                if (root.TryGetProperty("QueryResponse", out var qr))
                {
                    if (qr.TryGetProperty("Employee", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var first = arr.EnumerateArray().FirstOrDefault();
                        if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("Id", out var jid))
                            return jid.GetString();
                    }
                }

                // { "Employee": { "Id": "123", ... } }
                if (root.TryGetProperty("Employee", out var emp) && emp.ValueKind == JsonValueKind.Object)
                {
                    if (emp.TryGetProperty("Id", out var jid2))
                        return jid2.GetString();
                }
            }
            catch { /* no-json */ }

            // XML
            try
            {
                var x = XDocument.Parse(payload);
                // Intenta Employee/Id
                var id = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Employee")
                              ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Id")?.Value;
                if (!string.IsNullOrWhiteSpace(id)) return id;

                // Intenta QueryResponse/Employee/Id
                id = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "QueryResponse")
                     ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Employee")
                     ?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Id")?.Value;
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
            catch { /* no-xml */ }

            return null;
        }
    }
}
