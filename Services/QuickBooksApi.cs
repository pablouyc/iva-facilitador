using System.Dynamic;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaFacilitador.Services
{
    public interface IQuickBooksApi
    {
        Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default);
        Task<List<dynamic>> GetSalesTransactionsAsync(string accessToken, string realmId, DateTime fechaInicio, DateTime fechaFin, CancellationToken ct = default);
    }

    public class QuickBooksApi : IQuickBooksApi
    {
        private readonly IHttpClientFactory _http;
        private readonly IOptions<IntuitOAuthSettings> _settings;
        private readonly ILogger<QuickBooksApi> _logger;

        public QuickBooksApi(IHttpClientFactory http, IOptions<IntuitOAuthSettings> settings, ILogger<QuickBooksApi> logger)
        {
            _http = http;
            _settings = settings;
            _logger = logger;
        }

        public async Task<string?> GetCompanyNameAsync(string realmId, string accessToken, CancellationToken ct = default)
        {
            var host = (_settings.Value.Environment ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var direct = await GetCompanyNameFromCompanyInfoAsync(host, realmId, accessToken, ct);
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            var viaQuery = await GetCompanyNameViaQueryAsync(host, realmId, accessToken, ct);
            if (!string.IsNullOrWhiteSpace(viaQuery)) return viaQuery;

            _logger.LogWarning("[QBO] No se logró obtener CompanyName por ninguna vía para realmId={RealmId}", realmId);
            return null;
        }

        private async Task<string?> GetCompanyNameFromCompanyInfoAsync(string host, string realmId, string accessToken, CancellationToken ct)
        {
            var url = $"{host}/v3/company/{realmId}/companyinfo/{realmId}?minorversion=70";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] companyinfo fallo HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("CompanyInfo", out var ci)
                    && ci.TryGetProperty("CompanyName", out var nameProp)
                    && nameProp.ValueKind == JsonValueKind.String)
                {
                    var name = nameProp.GetString();
                    _logger.LogInformation("[QBO] CompanyInfo.CompanyName obtenido: {Name}", name);
                    return name;
                }

                _logger.LogWarning("[QBO] companyinfo sin CompanyName. Body: {Body}", body);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error en companyinfo");
                return null;
            }
        }

        private async Task<string?> GetCompanyNameViaQueryAsync(string host, string realmId, string accessToken, CancellationToken ct)
        {
            var query = "select * from CompanyInfo";
            var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var client = _http.CreateClient();
                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[QBO] query CompanyInfo fallo HTTP {Code}. Body: {Body}", (int)resp.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("QueryResponse", out var qr)
                    && qr.TryGetProperty("CompanyInfo", out var arr)
                    && arr.ValueKind == JsonValueKind.Array
                    && arr.GetArrayLength() > 0)
                {
                    var first = arr[0];
                    if (first.TryGetProperty("CompanyName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        var name = nameProp.GetString();
                        _logger.LogInformation("[QBO] Query CompanyInfo.CompanyName obtenido: {Name}", name);
                        return name;
                    }
                }

                _logger.LogWarning("[QBO] query CompanyInfo sin CompanyName. Body: {Body}", body);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QBO] Error en query CompanyInfo");
                return null;
            }
        }

        public async Task<List<dynamic>> GetSalesTransactionsAsync(string accessToken, string realmId, DateTime fechaInicio, DateTime fechaFin, CancellationToken ct = default)
        {
            var host = (_settings.Value.Environment ?? "production").Equals("sandbox", StringComparison.OrdinalIgnoreCase)
                ? "https://sandbox-quickbooks.api.intuit.com"
                : "https://quickbooks.api.intuit.com";

            var entities = new[] { "Invoice", "SalesReceipt", "CreditMemo" };
            var results = new List<dynamic>();

            foreach (var entity in entities)
            {
                var start = 1;
                const int max = 1000;
                var more = true;
                while (more)
                {
                    var query = $"SELECT Id, TxnDate, TxnTaxDetail, Line FROM {entity} WHERE TxnDate >= '{fechaInicio:yyyy-MM-dd}' AND TxnDate <= '{fechaFin:yyyy-MM-dd}' STARTPOSITION {start} MAXRESULTS {max}";
                    var url = $"{host}/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";

                    var body = await SendWithRetryAsync(url, accessToken, ct);

                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("QueryResponse", out var qr) &&
                        qr.TryGetProperty(entity, out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in arr.EnumerateArray())
                        {
                            dynamic dyn = new ExpandoObject();
                            var dict = (IDictionary<string, object?>)dyn;

                            dict["Entity"] = entity;

                            if (item.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String)
                                dict["Id"] = id.GetString();
                            if (item.TryGetProperty("TxnDate", out var txnDate) && txnDate.ValueKind == JsonValueKind.String)
                                dict["TxnDate"] = txnDate.GetString();
                            if (item.TryGetProperty("TxnTaxDetail", out var tax))
                                dict["TxnTaxDetail"] = JsonSerializer.Deserialize<dynamic>(tax.GetRawText());
                            if (item.TryGetProperty("Line", out var line))
                                dict["Line"] = JsonSerializer.Deserialize<dynamic>(line.GetRawText());

                            results.Add(dyn);
                        }

                        if (arr.GetArrayLength() == max)
                        {
                            start += max;
                        }
                        else
                        {
                            more = false;
                        }
                    }
                    else
                    {
                        more = false;
                    }
                }
            }

            return results;
        }

        private async Task<string> SendWithRetryAsync(string url, string accessToken, CancellationToken ct)
        {
            const int maxRetries = 5;
            var client = _http.CreateClient();

            for (var attempt = 0; ; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException("Token expirado");

                if ((int)resp.StatusCode == 429)
                {
                    if (attempt >= maxRetries)
                        throw new HttpRequestException("Rate limit excedido");
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, ct);
                    continue;
                }

                if ((int)resp.StatusCode >= 500)
                {
                    if (attempt >= maxRetries)
                        throw new HttpRequestException($"Error del servidor {(int)resp.StatusCode}");
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, ct);
                    continue;
                }

                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"Error HTTP {(int)resp.StatusCode}: {body}");

                return body;
            }
        }
    }
}
